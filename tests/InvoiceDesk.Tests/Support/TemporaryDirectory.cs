namespace InvoiceDesk.Tests.Support;

internal sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("invoice-desk-test-").FullName;

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
