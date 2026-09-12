using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace RoomBook.Architecture.Tests.Rules;

/// <summary>What the boundary rules need to know about a compiled assembly.</summary>
public sealed record AssemblyFacts(
    string Name,
    IReadOnlyList<string> ReferencedAssemblies,
    IReadOnlyList<string> ReferencedMembers,
    IReadOnlyList<string> WritableStaticFields);

/// <summary>A property on a wire contract type, used by the FD-5 rule.</summary>
public sealed record PropertyFact(string DeclaringType, string PropertyName, string PropertyTypeAssembly);

/// <summary>
/// Extracts facts from compiled output rather than from source text. Method bodies are where an
/// ambient clock read hides, and a source scan is fooled by formatting; the compiled metadata shows
/// what the compiler actually emitted.
/// </summary>
public static class AssemblyFactsReader
{
    public static AssemblyFacts Read(Assembly assembly)
    {
        using FileStream stream = File.OpenRead(assembly.Location);
        using PEReader peReader = new(stream);
        MetadataReader metadata = peReader.GetMetadataReader();

        return new AssemblyFacts(
            assembly.GetName().Name ?? string.Empty,
            ReadAssemblyReferences(metadata),
            ReadMemberReferences(metadata),
            ReadWritableStaticFields(assembly));
    }

    public static IReadOnlyList<PropertyFact> ReadWireContractProperties(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal)
                || type.Name.EndsWith("Response", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => new PropertyFact(
                    type.FullName ?? type.Name,
                    property.Name,
                    property.PropertyType.Assembly.GetName().Name ?? string.Empty)))
            .ToList();

    private static IReadOnlyList<string> ReadAssemblyReferences(MetadataReader metadata) =>
        metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> ReadMemberReferences(MetadataReader metadata)
    {
        List<string> members = [];

        foreach (MemberReferenceHandle handle in metadata.MemberReferences)
        {
            MemberReference reference = metadata.GetMemberReference(handle);
            string parent = DescribeParent(metadata, reference.Parent);

            if (parent.Length == 0)
            {
                continue;
            }

            members.Add($"{parent}.{metadata.GetString(reference.Name)}");
        }

        return members.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();
    }

    private static string DescribeParent(MetadataReader metadata, EntityHandle parent)
    {
        if (parent.Kind is not HandleKind.TypeReference)
        {
            return string.Empty;
        }

        TypeReference type = metadata.GetTypeReference((TypeReferenceHandle)parent);
        string name = metadata.GetString(type.Name);
        string @namespace = type.Namespace.IsNil ? string.Empty : metadata.GetString(type.Namespace);

        return @namespace.Length == 0 ? name : $"{@namespace}.{name}";
    }

    /// <summary>
    /// Static fields we could have written. Compiler-generated members are skipped: lambda caching
    /// emits writable statics of its own, and FD-4 is a rule about our code, not about the compiler's.
    /// </summary>
    private static IReadOnlyList<string> ReadWritableStaticFields(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => !IsCompilerGenerated(type.FullName ?? type.Name))
            .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(field => !field.IsInitOnly && !field.IsLiteral)
            .Where(field => !IsCompilerGenerated(field.Name))
            .Select(field => $"{field.DeclaringType?.FullName}.{field.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static bool IsCompilerGenerated(string name) => name.Contains('<', StringComparison.Ordinal);
}
