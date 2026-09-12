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
    public void BannedPackageReferences_ForEveryProjectInTheRepository_FindsNone()
    {
        List<string> violations = Repository.AllProjectFilePaths()
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
    public void AllProjectFilePaths_FindsEveryProjectInTheSolution()
    {
        IReadOnlyList<string> paths = Repository.AllProjectFilePaths();

        // Six projects: three under src, three test projects. A sweep that finds nothing would
        // report success for every rule, so the count is part of the contract.
        Assert.Equal(6, paths.Count);
    }
}
