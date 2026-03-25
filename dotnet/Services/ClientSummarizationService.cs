using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using ClientSummarization.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace ClientSummarization.Services;

/// <summary>
/// .NET port of the DSPy ChainOfThought(ClientSummarization) module from
/// summarization_messages.ipynb. Preserves the exact system prompt, field
/// descriptions, and reasoning+summary output structure.
/// </summary>
public sealed class ClientSummarizationService
{
    // -------------------------------------------------------------------------
    // 1. CreateKernel
    //    Reads env vars, validates all are present, builds SK Kernel with
    //    AzureOpenAIChatCompletion registered under serviceId "azure-openai".
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads AZURE_OPENAI_4_1_KEY, AZURE_LANGUAGE_ENDPOINT, AZURE_OPENAI_DEPLOYMENT_NAME,
    /// AZURE_API_VERSION from process environment. Validates all are present.
    /// Builds and returns a Kernel with AzureOpenAIChatCompletion service.
    /// </summary>
    public static Kernel CreateKernel()
    {
        var apiKey         = Environment.GetEnvironmentVariable("AZURE_OPENAI_4_1_KEY");
        var endpoint       = Environment.GetEnvironmentVariable("AZURE_LANGUAGE_ENDPOINT");
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
        var apiVersion     = Environment.GetEnvironmentVariable("AZURE_API_VERSION");

        var missing = new Dictionary<string, string?>
        {
            ["AZURE_OPENAI_4_1_KEY"]        = apiKey,
            ["AZURE_LANGUAGE_ENDPOINT"]      = endpoint,
            ["AZURE_OPENAI_DEPLOYMENT_NAME"] = deploymentName,
            ["AZURE_API_VERSION"]            = apiVersion,
        }
        .Where(kv => string.IsNullOrWhiteSpace(kv.Value))
        .Select(kv => kv.Key)
        .OrderBy(k => k)
        .ToList();

        if (missing.Count > 0)
            throw new InvalidOperationException(
                "Missing required Azure OpenAI settings: " + string.Join(", ", missing));

        var clientOptions = new AzureOpenAIClientOptions();
        var azureClient = new AzureOpenAIClient(
            new Uri(endpoint!),
            new AzureKeyCredential(apiKey!),
            clientOptions);

        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: deploymentName!,
            azureOpenAIClient: azureClient,
            serviceId: "azure-openai");

        return builder.Build();
    }

    // -------------------------------------------------------------------------
    // 2. BuildUserPrompt
    //    Mirrors the DSPy input-field label format produced by dspy.inspect_history.
    //    Labels match the field names in ClientSummarization exactly.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Formats the three input fields into a labelled user message matching
    /// the DSPy inspect_history prompt structure.
    /// </summary>
    public static string BuildUserPrompt(
        string financialData,
        string notes,
        string emailConversation)
    {
        return
            $"""
            Financial Data: {financialData}

            Notes: {notes}

            Email Conversation: {emailConversation}
            """.Trim();
    }

    // -------------------------------------------------------------------------
    // 3. ParseStructuredResponse
    //    SK .NET places the structured JSON in ChatMessageContent.Content when
    //    ResponseFormat = typeof(T) is used. Falls back to iterating .Items.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extracts and deserialises a ClientSummarizationResult from a ChatMessageContent.
    /// Primary path: message.Content (JSON string from structured output).
    /// Fallback: iterate message.Items for TextContent items.
    /// Throws if no structured payload is found.
    /// </summary>
    public static ClientSummarizationResult ParseStructuredResponse(ChatMessageContent message)
    {
        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            var result = TryDeserialize(message.Content);
            if (result is not null) return result;
        }

        foreach (var item in message.Items ?? Enumerable.Empty<KernelContent>())
        {
            if (item is TextContent tc && !string.IsNullOrWhiteSpace(tc.Text))
            {
                var result = TryDeserialize(tc.Text);
                if (result is not null) return result;
            }
        }

        throw new InvalidOperationException(
            "Semantic Kernel response did not contain a structured payload from the model");
    }

    // -------------------------------------------------------------------------
    // 4. SummarizeAsync
    //    Async core: creates kernel, builds ChatHistory with system + user message,
    //    calls GetChatMessageContentsAsync with structured output settings,
    //    parses and validates response, returns as dict (model_dump equivalent).
    //    Equivalent to:
    //      summarizer_cot = dspy.ChainOfThought(ClientSummarization)
    //      response = summarizer_cot(financial_data=..., notes=..., email_conversation=...)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Generates a client AR summary using chain-of-thought reasoning.
    /// Uses OpenAIPromptExecutionSettings with ResponseFormat = typeof(ClientSummarizationResult)
    /// (SKEXP0010) to request structured output, temperature=0.2.
    /// </summary>
    public static async Task<Dictionary<string, object?>> SummarizeAsync(
        string financialData,
        string notes,
        string emailConversation,
        double temperature = 0.2,
        int maxTokens = 600,
        CancellationToken cancellationToken = default)
    {
        var kernel      = CreateKernel();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature    = temperature,
            MaxTokens      = maxTokens,
            ResponseFormat = typeof(ClientSummarizationResult),
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(Constants.SystemPrompt);
        chatHistory.AddUserMessage(BuildUserPrompt(financialData, notes, emailConversation));

        var responses = await chatService.GetChatMessageContentsAsync(
            chatHistory:       chatHistory,
            executionSettings: settings,
            kernel:            kernel,
            cancellationToken: cancellationToken);

        if (responses is null || responses.Count == 0)
            throw new InvalidOperationException("Semantic Kernel returned no response content");

        var validated = ParseStructuredResponse(responses[0]);
        validated.Validate();

        return validated.ToDictionary();
    }

    // -------------------------------------------------------------------------
    // 5. Summarize  (sync wrapper — equivalent of asyncio.run(...))
    // -------------------------------------------------------------------------

    /// <summary>
    /// Synchronous entry point. Safe in a console app (no SynchronizationContext).
    /// For ASP.NET Core, use SummarizeAsync instead.
    /// </summary>
    public static Dictionary<string, object?> Summarize(
        string financialData,
        string notes,
        string emailConversation,
        double temperature = 0.2,
        int maxTokens = 600)
    {
        return SummarizeAsync(financialData, notes, emailConversation, temperature, maxTokens)
            .GetAwaiter()
            .GetResult();
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static ClientSummarizationResult? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ClientSummarizationResult>(json, s_jsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
