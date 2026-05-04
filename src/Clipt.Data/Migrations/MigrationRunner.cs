namespace Clipt.Data.Migrations;

public sealed class MigrationRunner
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
