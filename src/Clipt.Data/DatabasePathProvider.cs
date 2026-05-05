namespace Clipt.Data;

public sealed class DatabasePathProvider
{
    private readonly string? _overridePath;

    public DatabasePathProvider(string? overridePath = null)
    {
        _overridePath = overridePath;

        var directory = Path.GetDirectoryName(GetDatabasePath());
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }

    public string GetDatabasePath()
    {
        if (_overridePath is not null)
        {
            return _overridePath;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Clipt", "clipt.db");
    }
}
