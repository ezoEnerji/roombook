using System.Reflection;
using RoomBook.Architecture.Fixtures;
using RoomBook.Architecture.Tests.Rules;
using RoomBook.Domain.Rooms;

namespace RoomBook.Architecture.Tests;

/// <summary>
/// Runs the boundary rules against <c>RoomBook.Architecture.Fixtures</c>, an assembly that breaks
/// them on purpose. The synthetic-fact tests prove the rules reason correctly; these prove the
/// metadata reader actually sees a violation in compiled output, which is the half of AC-2 that a
/// hand-built fact list cannot demonstrate.
/// </summary>
public sealed class CompiledViolationTests
{
    private static Assembly FixtureAssembly => typeof(DeliberateViolations).Assembly;

    private static AssemblyFacts FixtureFacts => AssemblyFactsReader.Read(FixtureAssembly);

    [Fact]
    public void AspNetCoreReferences_ForTheViolatingAssembly_FindsTheWebFramework()
    {
        IReadOnlyList<string> violations = BoundaryRules.AspNetCoreReferences(FixtureFacts);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void AmbientClockReads_ForTheViolatingAssembly_FindsBothReads()
    {
        IReadOnlyList<string> violations = BoundaryRules.AmbientClockReads(FixtureFacts);

        Assert.Contains("System.DateTime.get_UtcNow", violations);
        Assert.Contains("System.DateTimeOffset.get_Now", violations);
    }

    [Fact]
    public void MutableStaticState_ForTheViolatingAssembly_FindsTheWritableField()
    {
        IReadOnlyList<string> violations = BoundaryRules.MutableStaticState(FixtureFacts);

        Assert.Contains("RoomBook.Architecture.Fixtures.DeliberateViolations.CallCount", violations);
    }

    [Fact]
    public void ServiceLocatorUses_ForTheViolatingAssembly_FindsTheResolution()
    {
        IReadOnlyList<string> violations = BoundaryRules.ServiceLocatorUses(FixtureFacts);

        Assert.NotEmpty(violations);
    }

    [Fact]
    public void DomainTypesOnTheWire_ForTheViolatingAssembly_FindsTheLeakedType()
    {
        IReadOnlyList<PropertyFact> properties = AssemblyFactsReader.ReadWireContractProperties(FixtureAssembly);

        IReadOnlyList<string> violations =
            BoundaryRules.DomainTypesOnTheWire(properties, typeof(Room).Assembly.GetName().Name!);

        Assert.Contains("RoomBook.Architecture.Fixtures.LeakyResponse.Room", violations);
    }
}
