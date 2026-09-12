using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace RoomBook.Api.Tests.Bookings;

/// <summary>
/// Shared helpers for the booking endpoint tests: raw JSON bodies (so a malformed request can be
/// expressed exactly as a client would send it) and the fixed clock the time-dependent rules need.
/// </summary>
internal static class BookingRequests
{
    /// <summary>Ada, capacity 4, open 09:00–18:00 Europe/Istanbul.</summary>
    public const string AdaId = "0192a1b2-c3d4-7a01-8b01-000000000001";

    /// <summary>Boğaziçi, capacity 12.</summary>
    public const string BogaziciId = "0192a1b2-c3d4-7a02-8b02-000000000002";

    /// <summary>2026-09-14 09:00 in Istanbul, so 10:00 local is 07:00Z.</summary>
    public static DateTimeOffset Now { get; } = new(2026, 9, 14, 6, 0, 0, TimeSpan.Zero);

    public static FakeTimeProvider Clock() => new(Now);

    public static string Body(
        string roomId = AdaId,
        string title = "Sprint review",
        string organizer = "Abdullah",
        string start = "2026-09-14T07:00:00Z",
        string end = "2026-09-14T08:00:00Z",
        int attendeeCount = 4) =>
        $$"""
        {"roomId":"{{roomId}}","title":"{{title}}","organizer":"{{organizer}}","start":"{{start}}","end":"{{end}}","attendeeCount":{{attendeeCount}}}
        """;

    public static Task<HttpResponseMessage> PostAsync(HttpClient client, string json, CancellationToken cancellationToken) =>
        client.PostAsync(
            new Uri("/bookings", UriKind.Relative),
            new StringContent(json, Encoding.UTF8, "application/json"),
            cancellationToken);

    public static async Task<string> CodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        return document.RootElement.GetProperty("code").GetString() ?? string.Empty;
    }
}
