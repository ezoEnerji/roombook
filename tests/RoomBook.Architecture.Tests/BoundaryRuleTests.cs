using System.Reflection;
using RoomBook.Api.Rooms;
using RoomBook.Application.Rooms;
using RoomBook.Architecture.Tests.Rules;
using RoomBook.Domain.Rooms;

namespace RoomBook.Architecture.Tests;

/// <summary>
/// FD-2 to FD-5, each rule run against the real assemblies and against synthetic facts that break
/// it (AC-2).
/// </summary>
public sealed class BoundaryRuleTests
{
    private static Assembly DomainAssembly => typeof(Room).Assembly;

    private static Assembly ApplicationAssembly => typeof(IRoomRepository).Assembly;

    private static Assembly ApiAssembly => typeof(RoomResponse).Assembly;

    private static IReadOnlyList<Assembly> ProductionAssemblies =>
        [DomainAssembly, ApplicationAssembly, ApiAssembly];

    private static IReadOnlyList<Assembly> InwardAssemblies => [DomainAssembly, ApplicationAssembly];

    private static AssemblyFacts FactsWith(
        IReadOnlyList<string>? referencedAssemblies = null,
        IReadOnlyList<string>? referencedMembers = null,
        IReadOnlyList<string>? writableStaticFields = null) =>
        new(
            "Synthetic",
            referencedAssemblies ?? [],
            referencedMembers ?? [],
            writableStaticFields ?? []);

    [Fact]
    public void AspNetCoreReferences_ForDomainAndApplication_FindsNone()
    {
        List<string> violations = InwardAssemblies
            .SelectMany(assembly => BoundaryRules.AspNetCoreReferences(AssemblyFactsReader.Read(assembly))
                .Select(reference => $"{assembly.GetName().Name} -> {reference}"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void AspNetCoreReferences_WhenALayerReferencesTheWebFramework_FindsIt()
    {
        AssemblyFacts facts = FactsWith(referencedAssemblies: ["System.Runtime", "Microsoft.AspNetCore.Http.Abstractions"]);

        Assert.Equal(["Microsoft.AspNetCore.Http.Abstractions"], BoundaryRules.AspNetCoreReferences(facts));
    }

    [Fact]
    public void AmbientClockReads_ForEveryProductionAssembly_FindsNone()
    {
        List<string> violations = ProductionAssemblies
            .SelectMany(assembly => BoundaryRules.AmbientClockReads(AssemblyFactsReader.Read(assembly))
                .Select(member => $"{assembly.GetName().Name} -> {member}"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void AmbientClockReads_WhenCodeReadsUtcNow_FindsIt()
    {
        AssemblyFacts facts = FactsWith(referencedMembers:
        [
            "System.Console.WriteLine",
            "System.DateTime.get_UtcNow",
            "System.DateTimeOffset.get_Now",
        ]);

        Assert.Equal(
            ["System.DateTime.get_UtcNow", "System.DateTimeOffset.get_Now"],
            BoundaryRules.AmbientClockReads(facts));
    }

    [Fact]
    public void MutableStaticState_ForEveryProductionAssembly_FindsNone()
    {
        List<string> violations = ProductionAssemblies
            .SelectMany(assembly => BoundaryRules.MutableStaticState(AssemblyFactsReader.Read(assembly)))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void MutableStaticState_WhenAWritableStaticFieldExists_FindsIt()
    {
        AssemblyFacts facts = FactsWith(writableStaticFields: ["RoomBook.Domain.Rooms.Room.Cache"]);

        Assert.Equal(["RoomBook.Domain.Rooms.Room.Cache"], BoundaryRules.MutableStaticState(facts));
    }

    [Fact]
    public void ServiceLocatorUses_ForEveryProductionAssembly_FindsNone()
    {
        List<string> violations = ProductionAssemblies
            .SelectMany(assembly => BoundaryRules.ServiceLocatorUses(AssemblyFactsReader.Read(assembly))
                .Select(member => $"{assembly.GetName().Name} -> {member}"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ServiceLocatorUses_WhenCodeResolvesFromTheContainer_FindsIt()
    {
        AssemblyFacts facts = FactsWith(referencedMembers:
            ["Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService"]);

        Assert.Single(BoundaryRules.ServiceLocatorUses(facts));
    }

    [Fact]
    public void DomainTypesOnTheWire_ForTheApiContracts_FindsNone()
    {
        IReadOnlyList<PropertyFact> properties = AssemblyFactsReader.ReadWireContractProperties(ApiAssembly);

        Assert.NotEmpty(properties);
        Assert.Empty(BoundaryRules.DomainTypesOnTheWire(properties, DomainAssembly.GetName().Name!));
    }

    [Fact]
    public void DomainTypesOnTheWire_WhenAContractExposesADomainType_FindsIt()
    {
        IReadOnlyList<PropertyFact> properties =
        [
            new("RoomBook.Api.Rooms.RoomResponse", "Name", "System.Private.CoreLib"),
            new("RoomBook.Api.Rooms.RoomResponse", "Hours", "RoomBook.Domain"),
        ];

        Assert.Equal(
            ["RoomBook.Api.Rooms.RoomResponse.Hours"],
            BoundaryRules.DomainTypesOnTheWire(properties, "RoomBook.Domain"));
    }

    [Fact]
    public void ProductionAssemblies_CoverEveryProjectUnderSrc()
    {
        string separator = Path.DirectorySeparatorChar.ToString();

        List<string> projectsUnderSrc = Repository.AllProjectFilePaths()
            .Where(path => path.Contains($"{separator}src{separator}", StringComparison.Ordinal))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        List<string> inspected = ProductionAssemblies
            .Select(assembly => assembly.GetName().Name ?? string.Empty)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Adding a project under src/ must fail this test until the boundary rules are told to
        // inspect it — otherwise a new layer would quietly escape every rule above.
        Assert.Equal(projectsUnderSrc, inspected);
    }

    [Fact]
    public void ProductionAssemblies_ExcludeTestAssemblies()
    {
        Assert.DoesNotContain(
            ProductionAssemblies,
            assembly => (assembly.GetName().Name ?? string.Empty).EndsWith(".Tests", StringComparison.Ordinal));
    }
}
