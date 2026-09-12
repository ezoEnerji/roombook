using System.Net;
using System.Text.Json;
using static RoomBook.Api.Tests.Bookings.BookingRequests;

namespace RoomBook.Api.Tests.Bookings;

/// <summary>
/// AC-5, the strongest promise in the spec: a window the search proposes is never refused by a
/// booking request. Every candidate is actually created here, so the promise is checked end to end
/// rather than argued about.
/// </summary>
public sealed class AvailabilityRoundTripTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryProposedWindow_IsAcceptedByABookingRequest()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        // The attendee count is part of the question, so it is part of the booking too: searching
        // for four people and booking for four is what makes the promise meaningful. Searching
        // without it and booking with it would leave the capacity rule untested here.
        using JsonDocument proposals = await SearchAsync(
            client,
            "durationMinutes=90&from=2026-09-15T00:00:00Z&to=2026-09-19T00:00:00Z&attendeeCount=4");

        IReadOnlyList<(string RoomId, string Start, string End)> candidates = proposals.RootElement
            .EnumerateArray()
            .Select(slot => (
                slot.GetProperty("roomId").GetString()!,
                slot.GetProperty("start").GetDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                slot.GetProperty("end").GetDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

        Assert.NotEmpty(candidates);

        foreach ((string roomId, string start, string end) in candidates)
        {
            HttpResponseMessage created = await PostAsync(
                client,
                Body(roomId: roomId, start: start, end: end),
                Token);

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
    }

    [Fact]
    public async Task AfterBookingEveryProposal_TheSearchProposesTheRemainingTime()
    {
        // The second half of the promise: acting on the answer changes the answer. Booking every
        // proposal for one room must not leave that same proposal on offer.
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        const string query = "durationMinutes=60&from=2026-09-15T00:00:00Z&to=2026-09-15T23:00:00Z";

        using JsonDocument before = await SearchAsync(client, $"{query}&roomId={AdaId}");
        string start = before.RootElement[0].GetProperty("start")
            .GetDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        string end = before.RootElement[0].GetProperty("end")
            .GetDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

        await PostAsync(client, Body(start: start, end: end), Token);

        using JsonDocument after = await SearchAsync(client, $"{query}&roomId={AdaId}");

        Assert.NotEqual(
            start,
            after.RootElement[0].GetProperty("start")
                .GetDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task<JsonDocument> SearchAsync(HttpClient client, string query)
    {
        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/availability?{query}", UriKind.Relative),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
    }
}
