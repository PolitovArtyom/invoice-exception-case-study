using InvoiceDesk.Core.Cases;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Web.Api;
using InvoiceDesk.Web.CaseLoading;
using InvoiceDesk.Web.Mcp;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<InvoiceDatabase>(
    (services, options) =>
    {
        var config = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        string databasePath = config["DatabasePath"]
            ?? Path.Combine(environment.ContentRootPath, "invoice.sqlite");
        options.UseSqlite(InvoiceDatabase.ConnectionString(databasePath));
    }
);
builder.Services.AddSingleton<InvoiceEvaluator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CaseFolders>();
builder.Services.AddScoped<CaseRepository>();
builder.Services.AddScoped<CaseService>();
builder.Services.AddScoped<CaseReader>();
builder.Services.AddScoped<CasePdfs>();
builder.Services.AddScoped<CaseLoader>();
builder
    .Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<InvoiceTools>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<InvoiceDatabase>();
    Directory.CreateDirectory(
        Path.GetDirectoryName(database.Database.GetDbConnection().DataSource)!
    );
    database.Database.Migrate();
    scope.ServiceProvider.GetRequiredService<CaseLoader>().Load();
}

if (app.Environment.WebRootPath is not null)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("/cases/{*path:nonfile}", "index.html");
}
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapCaseEndpoints();
app.MapMcp("/mcp");
app.Run();

public partial class Program;
