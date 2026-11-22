using InvoiceDesk.Core.Model;
using InvoiceDesk.Core.Model.CompanyData;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InvoiceDesk.Core.Storage;

public sealed class InvoiceDatabase(DbContextOptions<InvoiceDatabase> options) : DbContext(options)
{
    internal DbSet<InvoiceCase> Cases => Set<InvoiceCase>();
    internal DbSet<InvoiceDocument> Documents => Set<InvoiceDocument>();
    internal DbSet<CompanyDataSnapshot> CompanyDataSnapshots => Set<CompanyDataSnapshot>();
    internal DbSet<CaseRevision> Revisions => Set<CaseRevision>();

    public static string ConnectionString(string databasePath) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            ForeignKeys = true,
        }.ToString();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var cases = builder.Entity<InvoiceCase>();
        cases.ToTable("Cases");
        cases.Ignore(row => row.Metadata);
        cases.Property(row => row.Origin).HasConversion<string>();
        cases.Property(row => row.AsOf).HasConversion<string>();
        cases.Property(row => row.Sources).HasConversion<InvoiceJsonConversion<CaseSources>>();

        var documents = builder.Entity<InvoiceDocument>();
        documents.ToTable("Documents");
        documents.Property(row => row.ReadingOrigin).HasConversion<string>();
        documents.Property(row => row.Facts).HasConversion<InvoiceJsonConversion<InvoiceFacts>>();
        documents
            .Property(row => row.Source)
            .HasConversion(new InvoiceJsonConversion<InvoiceSource?>());
        documents
            .HasOne<InvoiceCase>()
            .WithMany()
            .HasForeignKey(row => row.CaseId)
            .OnDelete(DeleteBehavior.Restrict);
        documents.HasIndex(row => new { row.CaseId, row.ImportRevision }).IsUnique();

        var snapshots = builder.Entity<CompanyDataSnapshot>();
        snapshots.ToTable("CompanyDataSnapshots");
        snapshots.Property(row => row.Data).HasConversion<InvoiceJsonConversion<CompanyData>>();
        snapshots
            .HasOne<InvoiceCase>()
            .WithMany()
            .HasForeignKey(row => row.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        var revisions = builder.Entity<CaseRevision>();
        revisions.ToTable("Revisions");
        revisions.HasKey(row => new { row.CaseId, row.Number });
        revisions
            .Property(row => row.Action)
            .HasConversion<InvoiceJsonConversion<RevisionAction>>();
        revisions
            .Property(row => row.Result)
            .HasConversion(new InvoiceJsonConversion<Model.Results.InvoiceResult?>());
        revisions
            .Property(row => row.OriginalInvoice)
            .HasConversion(new InvoiceJsonConversion<OriginalInvoice?>());
        revisions
            .Property(row => row.DuplicateAttempt)
            .HasConversion(new InvoiceJsonConversion<DuplicateAttempt?>());
        revisions
            .HasOne<InvoiceCase>()
            .WithMany()
            .HasForeignKey(row => row.CaseId)
            .OnDelete(DeleteBehavior.Restrict);
        revisions
            .HasOne(row => row.Document)
            .WithMany()
            .HasForeignKey(row => row.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        revisions
            .HasOne(row => row.Snapshot)
            .WithMany()
            .HasForeignKey(row => row.SnapshotId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private sealed class InvoiceJsonConversion<T>()
        : ValueConverter<T, string>(
            value => InvoiceJson.Write(value),
            json => InvoiceJson.Read<T>(json)
        );
}
