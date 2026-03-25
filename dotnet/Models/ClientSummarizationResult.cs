using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ClientSummarization.Models;

/// <summary>
/// Mirrors the DSPy ChainOfThought(ClientSummarization) output fields.
/// Field descriptions are preserved verbatim from the DSPy OutputField(desc=...) annotations
/// so they appear in the JSON schema sent to the model for structured output.
/// Source: summarization_messages.ipynb, cell 15.
/// </summary>
public sealed class ClientSummarizationResult
{
    /// <summary>
    /// Chain-of-thought reasoning step added by dspy.ChainOfThought before producing the summary.
    /// </summary>
    [Description("Let's think step by step in order to ${produce the summary} ")]
    [JsonPropertyName("reasoning")]
    public required string Reasoning { get; init; }

    /// <summary>
    /// Final attorney-ready summary paragraph.
    /// </summary>
    [Description("Actionable summary of the Client across the invoices, notes and financial_data. Accurately mention dates, invoice numbers, amounts when summarizing notes and financial_data.  It should be helpful for the attorneys and provides a up-to-date detailed overview of the the account.")]
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    // ---------------------------------------------------------------------------
    // Validation — replicates Python Pydantic post-parse checks
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Validates that required fields are non-empty after deserialisation.
    /// Equivalent to Pydantic's @field_validator post-parse checks.
    /// Throws <see cref="InvalidOperationException"/> on any violation.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Reasoning))
            throw new InvalidOperationException("reasoning field must not be empty.");

        if (string.IsNullOrWhiteSpace(Summary))
            throw new InvalidOperationException("summary field must not be empty.");
    }

    /// <summary>
    /// Converts to a Dictionary — equivalent of Python's model_dump().
    /// Keys are snake_case to match the Python output.
    /// </summary>
    public Dictionary<string, object?> ToDictionary() => new()
    {
        ["reasoning"] = Reasoning,
        ["summary"]   = Summary,
    };
}
