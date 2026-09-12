using RoomBook.Architecture.Tests.Rules;

namespace RoomBook.Architecture.Tests;

/// <summary>
/// FD-1 and FD-6. Every rule is exercised twice: once against the real repository and once against
/// a synthetic project that breaks it. A rule that has only ever been seen to pass is not evidence
/// that it works (AC-2).
/// </summary>
public sealed class ProjectRuleTests
{
    private const string ProjectWithPackage = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="Serilog" Version="4.0.0" />
          </ItemGroup>
        </Project>
        """;

    private const string PropsWithPackage = """
        <Project>
          <ItemGroup>
            <PackageReference Include="Serilog" Version="4.0.0" />
          </ItemGroup>
        </Project>
        """;

    private const string ProjectWithBannedPackage = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="MediatR" Version="12.0.0" />
            <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void References_ForTheDomainProject_FindsNone()
    {
        IReadOnlyList<string> references =
            ProjectRules.References(Repository.ReadProjectFile("src/RoomBook.Domain/RoomBook.Domain.csproj"));

        Assert.Empty(references);
    }

    [Fact]
    public void References_WhenAProjectDeclaresAPackage_FindsIt()
    {
        IReadOnlyList<string> references = ProjectRules.References(ProjectWithPackage);

        Assert.Equal(["Serilog"], references);
    }

    [Fact]
    public void References_ForTheSharedBuildProps_FindsNone()
    {
        // Directory.Build.props applies to every project, so a package declared there reaches
        // RoomBook.Domain just as surely as one written in its own csproj.
        IReadOnlyList<string> references = ProjectRules.References(Repository.ReadProjectFile("Directory.Build.props"));

        Assert.Empty(references);
    }

    [Fact]
    public void References_WhenSharedBuildPropsDeclareAPackage_FindsIt()
    {
        IReadOnlyList<string> references = ProjectRules.References(PropsWithPackage);

        Assert.Equal(["Serilog"], references);
    }

    [Fact]
    public void BannedPackageReferences_ForEveryBuildFileInTheRepository_FindsNone()
    {
        List<string> violations = Repository.AllBuildFilePaths()
            .SelectMany(path => ProjectRules.BannedPackageReferences(File.ReadAllText(path))
                .Select(package => $"{Path.GetFileName(path)}: {package}"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void BannedPackageReferences_WhenAProjectDeclaresABannedPackage_FindsThemAll()
    {
        IReadOnlyList<string> violations = ProjectRules.BannedPackageReferences(ProjectWithBannedPackage);

        Assert.Equal(["MediatR", "Microsoft.EntityFrameworkCore.Sqlite"], violations);
    }

    [Fact]
    public void AllBuildFilePaths_FindsEveryProjectAndSharedBuildFile()
    {
        IReadOnlyList<string> projects = Repository.AllProjectFilePaths();
        IReadOnlyList<string> buildFiles = Repository.AllBuildFilePaths();

        // Seven projects: three under src, three test projects, and the deliberately violating
        // fixture. Plus Directory.Build.props. A sweep that finds nothing would report success for
        // every rule, so the counts are part of the contract.
        Assert.True(
            projects.Count == 7,
            $"Expected 7 projects, found {projects.Count}: {StringsOf(projects)}");
        Assert.True(
            buildFiles.Count == 8,
            $"Expected 8 build files, found {buildFiles.Count}: {StringsOf(buildFiles)}");
    }

    /// <summary>
    /// Adding a project is supposed to fail the counts above. Listing what was found turns
    /// "expected 7, got 8" into a message that says which file arrived.
    /// </summary>
    private static string StringsOf(IReadOnlyList<string> paths) =>
        string.Join(", ", paths.Select(Path.GetFileName));
}
