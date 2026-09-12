using System.Text.Json;
using static RoomBook.Api.Tests.Bookings.BookingRequests;

namespace RoomBook.Api.Tests.Bookings;

/// <summary>
/// The first behaviour that spans two slices: nothing in the availability search knows about
/// cancellation, and nothing in cancellation knows about the search. They meet only in the store,
/// which is exactly why this needs a test of its own — it is the kind of connection that breaks
/// silently when either side changes.
/// </summary>
public sealed class CancellationFreesTimeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AfterCancelling_TheSearchProposesTheFreedTimeAgain()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        const string search = "durationMinutes=60&from=2026-09-15T00:00:00Z&to=2026-09-15T23:00:00Z";

        HttpResponseMessage created = await PostAsync(
            client,
            Body(start: "2026-09-15T06:00:00Z", end: "2026-09-15T07:00:00Z"),
            Token);

        // While the booking stands, the room's first free window starts when it ends.
        Assert.Equal(
            "2026-09-15T07:00:00+00:00",
            await FirstCandidateStartAsync(client, $"{search}&roomId={AdaId}"));

        await client.DeleteAsync(created.Headers.Location!, Token);

        // Once it is cancelled, the opening slot is on offer again.
        Assert.Equal(
            "2026-09-15T06:00:00+00:00",
            await FirstCandidateStartAsync(client, $"{search}&roomId={AdaId}"));
    }

    [Fact]
    public async Task AfterCancelling_TheFreedTimeCanBeBookedByAnotherOrganizer()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage created = await PostAsync(client, Body(organizer: "Abdullah"), Token);
        await client.DeleteAsync(created.Headers.Location!, Token);

        HttpResponseMessage second = await PostAsync(client, Body(organizer: "Ay\u015fe"), Token);

        Assert.Equal(System.Net.HttpStatusCode.Created, second.StatusCode);
    }

    private static async Task<string> FirstCandidateStartAsync(HttpClient client, string query)
    {
        using JsonDocument body = JsonDocument.Parse(
            await client.GetStringAsync(new Uri($"/availability?{query}", UriKind.Relative), Token));

        return body.RootElement[0].GetProperty("start").GetString() ?? string.Empty;
    }
}
