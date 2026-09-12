using System.Xml.Linq;

namespace RoomBook.Architecture.Tests.Rules;

/// <summary>
/// Inspection rules over a project file's XML. Pure functions on purpose: the same rule can be run
/// against the real repository and against a synthetic violating project, which is the only way to
/// know a rule actually detects anything (AC-2).
/// </summary>
public static class ProjectRules
{
    /// <summary>Package name prefixes banned by FD-6.</summary>
    public static IReadOnlyList<string> BannedPackagePrefixes { get; } =
    [
        "MediatR",
        "AutoMapper",
        "Newtonsoft.Json",
        "Microsoft.EntityFrameworkCore",
        "FluentAssertions",
    ];

    /// <summary>FD-1: every package and project reference declared by a project.</summary>
    public static IReadOnlyList<string> References(string projectXml) =>
        Includes(projectXml, element => element.Name.LocalName is "PackageReference" or "ProjectReference");

    /// <summary>FD-6: the references whose package name starts with a banned prefix.</summary>
    public static IReadOnlyList<string> BannedPackageReferences(string projectXml) =>
        Includes(projectXml, element => element.Name.LocalName is "PackageReference")
            .Where(IsBanned)
            .ToList();

    private static bool IsBanned(string packageName) =>
        BannedPackagePrefixes.Any(prefix => packageName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> Includes(string projectXml, Func<XElement, bool> predicate) =>
        XDocument.Parse(projectXml)
            .Descendants()
            .Where(predicate)
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Where(include => include.Length > 0)
            .ToList();
}
