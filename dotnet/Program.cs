using ClientSummarization.Services;
using DotNetEnv;

// ---------------------------------------------------------------------------
// Load .env first, then .env.local from the project root so local secrets can
// override shared defaults. DotNetEnv injects variables into process env,
// readable via Environment.GetEnvironmentVariable downstream.
// Project root is 4 levels above bin/Debug/net9.0/.
// ---------------------------------------------------------------------------
var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var envFile      = Path.Combine(projectRoot, ".env");
var localEnvFile = Path.Combine(projectRoot, ".env.local");

if (File.Exists(envFile))
    Env.Load(envFile);
else
    Console.Error.WriteLine($"[warn] .env not found at: {envFile}");

if (File.Exists(localEnvFile))
    Env.Load(localEnvFile);
else
    Console.Error.WriteLine($"[warn] .env.local not found at: {localEnvFile}");

// ---------------------------------------------------------------------------
// Demo entry point — calls Summarize with sample data and prints result.
//
// Usage:  dotnet run
//         dotnet ClientSummarization.dll
// ---------------------------------------------------------------------------
Console.WriteLine("Running ClientSummarization demo...");
Console.WriteLine(new string('-', 60));

// Replace with real data in production (e.g. loaded via data_formatter_csv_list_v2)
var financialData = """
    invoice_number,submitted_on,due_on,ar_amount,days_overdue,status
    INV-1001,2025-01-10,2025-02-10,5000.00,43,Overdue
    INV-1002,2025-02-01,2025-03-03,3200.00,22,Overdue
    Total,,,8200.00,,
    """;

var notes = """
    invoice_number,created_on,content
    INV-1001,2025-01-20,"Left voicemail for client regarding overdue balance."
    INV-1001,2025-02-05,"Client called back, said check is in the mail."
    INV-1002,2025-02-15,"Sent follow-up email on both invoices. No response."
    """;

var emailConversation = """
    client_id,sent_on,subject,body
    42,2025-02-06,Re: Invoice INV-1001,"Hi, we mailed the check last Friday. Should arrive by end of week."
    42,2025-02-15,Follow-up: INV-1001 and INV-1002,"We have not received payment for INV-1001 and INV-1002 remains due. Please advise."
    """;

try
{
    var result = ClientSummarizationService.Summarize(financialData, notes, emailConversation);

    Console.WriteLine("Reasoning:");
    Console.WriteLine(result["reasoning"]);
    Console.WriteLine();
    Console.WriteLine("Summary:");
    Console.WriteLine(result["summary"]);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[error] {ex.Message}");
    Environment.Exit(1);
}
