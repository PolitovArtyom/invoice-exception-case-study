using System.Text.Json;
using System.Text.Json.Nodes;

using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Core.UseCases;
using InvoiceDesk.Core.Validation;

using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Adapter;

public static class Adapter
{
    public static int Run(string[] args, TextReader stdin, TextWriter stdout)
    {
        if (args is ["--state-dir", var stateDir] && !string.IsNullOrWhiteSpace(stateDir))
            return Run(stdin, stdout, stateDir);

        return ValidationError(stdout, new InvoiceValidationException("/state-dir", "Usage: InvoiceDesk.Adapter --state-dir <directory>"));
    }

    public static int Run(TextReader stdin, TextWriter stdout, string stateDir)
    {
        try
        {
            return Submit(Parse(stdin), stdout, stateDir);
        }
        catch (InvoiceValidationException error)
        {
            return ValidationError(stdout, error);
        }
        catch (UnsupportedInvoiceCaseException error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static JsonNode Parse(TextReader stdin)
    {
        try
        {
            var input = JsonNode.Parse(stdin.ReadToEnd());
            AssignmentSchemas.Instance.Validate("step-input", input);
            return input!;
        }
        catch (JsonException)
        {
            throw new InvoiceValidationException("", "Expected one JSON step input.");
        }
    }

    private static int Submit(JsonNode input, TextWriter stdout, string stateDir)
    {
        if (input["proposals"] is not null || input["confirmations"] is not null)
            throw new UnsupportedInvoiceCaseException("proposals and confirmations");

        string caseId = input["case_id"]!.GetValue<string>();
        string asOf = input["as_of"]!.GetValue<string>();
        if (!input["step"]!.AsValue().TryGetValue<int>(out int step))
            throw new InvoiceValidationException("/step", "Step number is outside the supported integer range.");
        IntakeValidation.ValidateDate(asOf, "/as_of");

        var company = input["context"] is { } context ? IntakeValidation.ReadCompanyData(context, "/context") : null;
        var reading = input["document"] is { } document
            ? new InvoiceReading(IntakeValidation.ReadFacts(document["facts"], "/document/facts"),
                document["content_sha256"]!.GetValue<string>(), null, "runner")
            : null;

        Directory.CreateDirectory(stateDir);
        var databasePath = Path.Combine(stateDir, "state.sqlite");
        using var database = new InvoiceDatabase(new DbContextOptionsBuilder<InvoiceDatabase>()
            .UseSqlite(InvoiceDatabase.ConnectionString(databasePath)).Options);
        database.Database.Migrate();
        var updates = new CaseUpdates(new CaseRepository(database), new InvoiceEvaluator(), TimeProvider.System);
        var result = updates.SubmitCaseInputs(new(caseId, "assignment", asOf, []), step, company, reading);

        stdout.WriteLine(InvoiceJson.Write(result));
        Console.Error.WriteLine($"Saved {caseId} revision {result.InputRevision} via adapter.");
        return 0;
    }

    private static int ValidationError(TextWriter stdout, InvoiceValidationException error)
    {
        stdout.WriteLine(InvoiceJson.Write(error.Response));
        Console.Error.WriteLine($"Rejected adapter input ({error.Details.Count} validation errors).");
        return 2;
    }
}
