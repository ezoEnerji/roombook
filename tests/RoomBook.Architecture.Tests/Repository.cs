namespace RoomBook.Architecture.Tests;

/// <summary>
/// Locates the repository on disk so the project-file rules have something real to inspect.
/// A rule that silently finds no files would pass forever and prove nothing, so failure to find
/// the solution is an exception rather than an empty result.
/// </summary>
public static class Repository
{
    public static string Root { get; } = FindRoot();

    public static string ReadProjectFile(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public static IReadOnlyList<string> AllProjectFilePaths() => Find("*.csproj");

    /// <summary>
    /// Project files plus the shared build files. `Directory.Build.props` applies to every project,
    /// so a package reference declared there lands in RoomBook.Domain as surely as one written in
    /// its own csproj — a sweep that skipped it would leave an open door.
    /// </summary>
    public static IReadOnlyList<string> AllBuildFilePaths() =>
        Find("*.csproj").Concat(Find("Directory.*.props"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> Find(string pattern) =>
        Directory.GetFiles(Root, pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RoomBook.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find RoomBook.sln above '{AppContext.BaseDirectory}'. The architecture rules " +
            "cannot inspect project files, so they must fail rather than report success.");
    }
}
