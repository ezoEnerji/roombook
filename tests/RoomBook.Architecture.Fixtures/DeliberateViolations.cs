using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoomBook.Domain.Rooms;

namespace RoomBook.Architecture.Fixtures;

/// <summary>
/// Breaks FD-2, FD-3, FD-4 and FD-5 deliberately. The rules in RoomBook.Architecture.Tests are
/// asserted against this compiled assembly, which is what turns "the rule passes on clean code"
/// into "the rule detects a violation".
/// <para>
/// Do not clean this file up. Removing a violation does not fix anything — it makes the matching
/// architecture test fail, because the test's job is to find the violation here. Adjusting those
/// tests to match a cleaned-up fixture is the forbidden move: it turns the proofs off silently
/// (`docs/testing.md`, protected-tests rule).
/// </para>
/// </summary>
public static class DeliberateViolations
{
    /// <summary>FD-4: static mutable state.</summary>
    public static int CallCount;

    /// <summary>FD-3: reads the ambient clock instead of receiving the instant.</summary>
    public static DateTime Now() => DateTime.UtcNow;

    /// <summary>FD-3 again, through the other ambient clock.</summary>
    public static DateTimeOffset NowWithOffset() => DateTimeOffset.Now;

    /// <summary>FD-2: an inward layer touching the web framework.</summary>
    public static string? DescribePath(HttpContext context) => context.Request.Path.Value;

    /// <summary>FD-4: resolving a dependency from the container instead of receiving it.</summary>
    public static IThingTheContainerKnows Resolve(IServiceProvider provider) =>
        provider.GetRequiredService<IThingTheContainerKnows>();
}

/// <summary>Only exists so the service-locator violation above has something to resolve.</summary>
public interface IThingTheContainerKnows
{
}

/// <summary>FD-5: a wire contract exposing a domain type instead of its own fields.</summary>
public sealed record LeakyResponse(Guid Id, Room Room);
