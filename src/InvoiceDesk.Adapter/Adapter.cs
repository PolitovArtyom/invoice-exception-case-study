using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Core.Validation;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Adapter;

public static class Adapter
{
    public static int Run(string[] args, TextReader stdin, TextWriter stdout)
    {
        if (args is ["--state-dir", var stateDir] && !string.IsNullOrWhiteSpace(stateDir))
            return Run(stdin, stdout, stateDir);

        return ValidationError(
            stdout,
            new InvoiceValidationException(
                "/state-dir",
                "Usage: InvoiceDesk.Adapter --state-dir <directory>"
            )
        );
    }

    public static int Run(TextReader stdin, TextWriter stdout, string stateDir)
    {
        try
        {
            return Submit(IntakeValidation.ReadStep(stdin.ReadToEnd()), stdout, stateDir);
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

    private static int Submit(StepInput input, TextWriter stdout, string stateDir)
    {
        var reading = input.Document is { } document
            ? new InvoiceReading(document.Facts, document.ContentSha256, null, ReadingOrigin.Runner)
            : null;

        Directory.CreateDirectory(stateDir);
        var databasePath = Path.Combine(stateDir, "state.sqlite");
        using var database = new InvoiceDatabase(
            new DbContextOptionsBuilder<InvoiceDatabase>()
                .UseSqlite(InvoiceDatabase.ConnectionString(databasePath))
                .Options
        );
        database.Database.Migrate();
        var cases = new CaseService(
            new CaseRepository(database),
            new InvoiceEvaluator(),
            TimeProvider.System
        );
        var result = cases.SubmitCaseInputs(input, reading);

        stdout.WriteLine(InvoiceJson.Write(result));
        Console.Error.WriteLine(
            $"Saved {input.CaseId} revision {result.InputRevision} via adapter."
        );
        return 0;
    }

    private static int ValidationError(TextWriter stdout, InvoiceValidationException error)
    {
        stdout.WriteLine(InvoiceJson.Write(error.Response));
        Console.Error.WriteLine(
            $"Rejected adapter input ({error.Details.Count} validation errors)."
        );
        return 2;
    }
}
