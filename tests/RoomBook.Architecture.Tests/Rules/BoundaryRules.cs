namespace RoomBook.Architecture.Tests.Rules;

/// <summary>
/// Inspection rules over <see cref="AssemblyFacts"/>. Each returns the violations it found, so an
/// empty list is a pass and the failure message can name what went wrong.
/// </summary>
public static class BoundaryRules
{
    private static IReadOnlyList<string> AmbientClockMembers { get; } =
    [
        "System.DateTime.get_Now",
        "System.DateTime.get_UtcNow",
        "System.DateTime.get_Today",
        "System.DateTimeOffset.get_Now",
        "System.DateTimeOffset.get_UtcNow",
    ];

    private static IReadOnlyList<string> ServiceLocatorMembers { get; } =
    [
        "System.IServiceProvider.GetService",
        "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService",
        "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService",
    ];

    /// <summary>FD-2: references to the web framework from a layer that must not know about it.</summary>
    public static IReadOnlyList<string> AspNetCoreReferences(AssemblyFacts facts) =>
        facts.ReferencedAssemblies
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .ToList();

    /// <summary>FD-3: reads of the ambient clock instead of the injected <c>TimeProvider</c>.</summary>
    public static IReadOnlyList<string> AmbientClockReads(AssemblyFacts facts) =>
        facts.ReferencedMembers
            .Where(member => AmbientClockMembers.Contains(member, StringComparer.Ordinal))
            .ToList();

    /// <summary>FD-4: static mutable state.</summary>
    public static IReadOnlyList<string> MutableStaticState(AssemblyFacts facts) => facts.WritableStaticFields;

    /// <summary>FD-4: resolving dependencies from a container instead of receiving them.</summary>
    public static IReadOnlyList<string> ServiceLocatorUses(AssemblyFacts facts) =>
        facts.ReferencedMembers
            .Where(member => ServiceLocatorMembers.Contains(member, StringComparer.Ordinal))
            .ToList();

    /// <summary>FD-5: domain types exposed on the wire instead of the API's own DTOs.</summary>
    public static IReadOnlyList<string> DomainTypesOnTheWire(
        IReadOnlyList<PropertyFact> wireProperties,
        string domainAssemblyName) =>
        wireProperties
            .Where(property => string.Equals(property.PropertyTypeAssembly, domainAssemblyName, StringComparison.Ordinal))
            .Select(property => $"{property.DeclaringType}.{property.PropertyName}")
            .ToList();
}
