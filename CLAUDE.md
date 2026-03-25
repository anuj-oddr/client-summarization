# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AI-powered accounts receivable (AR) client summarization for law firms. Analyzes financial data, internal notes, and email conversations to generate attorney-focused AR status summaries using Azure OpenAI with chain-of-thought reasoning.

The repo contains two components:
- **`dotnet/`** — Production .NET 9.0 console app using Microsoft Semantic Kernel
- **`notebooks/`** — Python Jupyter notebooks for research and experimentation (the .NET app is a port of the DSPy ChainOfThought approach developed here)

## .NET Commands

```bash
# Build
dotnet build dotnet/

# Run (UseAppHost=false because the network-mounted filesystem doesn't support memory-mapped files)
dotnet run --project dotnet/
# or after build:
dotnet dotnet/bin/Debug/net9.0/ClientSummarization.dll
```

No test project exists yet.

## Environment Setup

Create `.env` and/or `.env.local` in the repo root. Required variables:

```
AZURE_OPENAI_4_1_KEY=
AZURE_LANGUAGE_ENDPOINT=
AZURE_OPENAI_DEPLOYMENT_NAME=
AZURE_API_VERSION=
```

`Program.cs` loads these from 4 levels up relative to the build output (`bin/Debug/net9.0`), which resolves to the repo root.

## Architecture

### .NET App

`ClientSummarizationService` is the core class:
- `CreateKernel()` — initializes Azure OpenAI via Semantic Kernel
- `BuildUserPrompt()` — formats three CSV inputs (financial data, notes, email conversation) into a labeled prompt
- `SummarizeAsync()` / `Summarize()` — orchestrates the call: builds chat history with system prompt, invokes the model with structured JSON output, parses and validates the response
- `ParseStructuredResponse()` — extracts JSON from `ChatMessageContent`

`ClientSummarizationResult` is the output model with two fields:
- `Reasoning` — chain-of-thought step-by-step thinking (SKEXP0010 structured output)
- `Summary` — final attorney-ready paragraph

The system prompt lives in `Constants.SystemPrompt` and defines the "Oddr Summarizer" persona with strict output rules: single paragraph, 4 sentences, smart bolding, specific date format, no fabrication.

Model settings: temperature 0.2, max tokens 600.

### Notebook Progression

The notebooks represent the R&D history:
1. **`summarization_messages.ipynb`** — original DSPy `ChainOfThought(ClientSummarization)` implementation; source of truth for the system prompt and field structure
2. **`sk_summarization.ipynb`** — Semantic Kernel Python port with chunked notes processing (production data path)
3. **`chunked_client_summary.ipynb`** — advanced chunking strategy for large note datasets: 40-line chunks → intermediate summaries → merge → final AR summary
4. **`data-exploration.ipynb`** — EDA on invoices, activities, and notes CSVs
5. **`summarization.ipynb`** — earlier experimental iteration
6. **`meta-summarization.ipynb`** — summary-of-summaries experiments


### Key Design Notes

- The `SKEXP0010`/`SKEXP0001` warnings are suppressed intentionally — Semantic Kernel structured output is experimental but is the chosen approach
- `ClientSummarizationService` and `ClientSummarizationResult` are `sealed`
- `SummarizeAsync` is the primary entry point; `Summarize()` is a sync wrapper using `.GetAwaiter().GetResult()` for the console app context
- Input is always three CSV strings passed directly as strings (not file paths)
