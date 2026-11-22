using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvoiceDesk.Core.Storage;

internal sealed class InvoiceDatabaseFactory : IDesignTimeDbContextFactory<InvoiceDatabase>
{
    public InvoiceDatabase CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<InvoiceDatabase>()
                .UseSqlite(InvoiceDatabase.ConnectionString("invoice.sqlite"))
                .Options
        );
}
