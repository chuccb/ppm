// =============================================================================
// Default writable paths for a zero-configuration server launch.
// =============================================================================
namespace PaperMan.Server;

/// <summary>
/// Resolves the default SQLite location without command-line arguments.
/// Development launches use the repository's <c>db/paperman.db</c>; published
/// builds use a writable <c>data/paperman.db</c> beside the application. Set
/// <c>PAPERMAN_DATABASE_PATH</c> only when an operator deliberately needs a
/// different persistent location.
/// </summary>
public static class ServerDataPaths
{
    private const string DatabasePathEnvironmentVariable = "PAPERMAN_DATABASE_PATH";
    private const string DatabaseFileName = "paperman.db";

    /// <summary>Gets the absolute path of the SQLite database to open or create.</summary>
    public static string GetDatabasePath()
    {
        string? configuredPath = Environment.GetEnvironmentVariable(DatabasePathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string? repositoryRoot = FindRepositoryRoot(Directory.GetCurrentDirectory())
            ?? FindRepositoryRoot(AppContext.BaseDirectory);
        string dataDirectory = repositoryRoot is not null
            ? Path.Combine(repositoryRoot, "db")
            : Path.Combine(AppContext.BaseDirectory, "data");
        return Path.Combine(dataDirectory, DatabaseFileName);
    }

    private static string? FindRepositoryRoot(string startingPath)
    {
        DirectoryInfo? directory;
        try
        {
            directory = new DirectoryInfo(Path.GetFullPath(startingPath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        while (directory is not null)
        {
            string schemaPath = Path.Combine(directory.FullName, "db", "schema.sql");
            string solutionPath = Path.Combine(directory.FullName, "server-cs", "PaperMan.slnx");
            if (File.Exists(schemaPath) && File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
