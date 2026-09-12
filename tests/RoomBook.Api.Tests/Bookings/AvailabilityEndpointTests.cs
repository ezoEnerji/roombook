using System.Net;
using System.Text.Json;
using RoomBook.Domain.Shared;
using static RoomBook.Api.Tests.Bookings.BookingRequests;

namespace RoomBook.Api.Tests.Bookings;

public sealed class AvailabilityEndpointTests
{
    private const string Tuesday = "from=2026-09-15T00:00:00Z&to=2026-09-15T23:00:00Z";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_WhenEveryRoomIsFree_ProposesOneWindowPerRoomOrderedByTime()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        using JsonDocument body = await SearchAsync(client, $"durationMinutes=60&{Tuesday}");

        // Kapadokya opens at 08:30 local, half an hour before the others, so it comes first.
        // Ada and Boğaziçi tie on time and break by name, ordinally.
        Assert.Equal(
            ["Kapadokya", "Ada", "Boğaziçi"],
            body.RootElement.EnumerateArray().Select(slot => slot.GetProperty("roomName").GetString()));

        JsonElement first = body.RootElement[0];
        Assert.Equal("2026-09-15T05:30:00+00:00", first.GetProperty("start").GetString());
        Assert.Equal("2026-09-15T06:30:00+00:00", first.GetProperty("end").GetString());
    }

    [Fact]
    public async Task Get_WhenNarrowedToOneRoom_ProposesThatRoomOnly()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        using JsonDocument body = await SearchAsync(client, $"durationMinutes=60&{Tuesday}&roomId={AdaId}");

        JsonElement only = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal("Ada", only.GetProperty("roomName").GetString());
    }

    [Fact]
    public async Task Get_WhenAnAttendeeCountIsGiven_LeavesOutRoomsThatAreTooSmall()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        // Ada holds 4, Boğaziçi 12, Kapadokya 24. Twelve people fit in two of them, and a room
        // whose capacity exactly equals the group is included.
        using JsonDocument body = await SearchAsync(client, $"durationMinutes=60&{Tuesday}&attendeeCount=12");

        Assert.Equal(
            ["Kapadokya", "Boğaziçi"],
            body.RootElement.EnumerateArray().Select(slot => slot.GetProperty("roomName").GetString()));
    }

    [Fact]
    public async Task Get_AfterABookingIsMade_ProposesTheWindowThatStartsWhenItEnds()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        await PostAsync(client, Body(start: "2026-09-15T06:00:00Z", end: "2026-09-15T07:00:00Z"), Token);

        using JsonDocument body = await SearchAsync(client, $"durationMinutes=60&{Tuesday}&roomId={AdaId}");

        JsonElement only = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal("2026-09-15T07:00:00+00:00", only.GetProperty("start").GetString());
    }

    [Fact]
    public async Task Get_WhenTheRoomIsFullyBooked_Returns200AndAnEmptyList()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        foreach ((string start, string end) in new[]
        {
            ("2026-09-15T06:00:00Z", "2026-09-15T10:00:00Z"),
            ("2026-09-15T10:00:00Z", "2026-09-15T14:00:00Z"),
            ("2026-09-15T14:00:00Z", "2026-09-15T15:00:00Z"),
        })
        {
            HttpResponseMessage created = await PostAsync(client, Body(start: start, end: end), Token);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/availability?durationMinutes=60&{Tuesday}&roomId={AdaId}", UriKind.Relative),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Get_WhenTheWindowIsEntirelyInThePast_Returns200AndAnEmptyList()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri(
                "/availability?durationMinutes=60&from=2026-09-01T00:00:00Z&to=2026-09-02T00:00:00Z",
                UriKind.Relative),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Get_WhenTheRoomDoesNotExist_Returns404()
    {
        HttpResponseMessage response = await RefusedAsync(
            $"durationMinutes=60&{Tuesday}&roomId=0192a1b2-c3d4-7aff-8bff-0000000000ff");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.RoomNotFound, await CodeAsync(response, Token));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(241)]
    public async Task Get_WhenTheRequestedLengthCouldNeverBeBooked_Returns422(int minutes)
    {
        // Refused rather than answered with an empty list: "no booking may be this long" and
        // "nothing is free" mean different things to a caller.
        HttpResponseMessage response = await RefusedAsync($"durationMinutes={minutes}&{Tuesday}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.DurationOutOfRange, await CodeAsync(response, Token));
    }

    [Theory]
    [InlineData("from=2026-09-15T00:00:00Z&to=2026-09-15T23:00:00Z")]
    [InlineData("durationMinutes=60&to=2026-09-15T23:00:00Z")]
    [InlineData("durationMinutes=60&from=2026-09-15T00:00:00Z")]
    [InlineData("durationMinutes=sixty&from=2026-09-15T00:00:00Z&to=2026-09-15T23:00:00Z")]
    [InlineData("durationMinutes=60&from=2026-09-15T03:00:00%2B03:00&to=2026-09-15T23:00:00Z")]
    [InlineData("durationMinutes=60&from=2026-09-15T23:00:00Z&to=2026-09-15T00:00:00Z")]
    [InlineData("durationMinutes=60&from=2026-09-15T00:00:00Z&to=2026-10-20T00:00:00Z")]
    [InlineData("durationMinutes=60&from=2026-09-15T00:00:00Z&to=2026-09-15T23:00:00Z&attendeeCount=0")]
    [InlineData("durationMinutes=60&from=2026-09-15T00:00:00Z&to=2026-09-15T23:00:00Z&roomId=not-a-guid")]
    public async Task Get_WhenTheQueryIsMalformed_Returns400(string query)
    {
        HttpResponseMessage response = await RefusedAsync(query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.RequestInvalid, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Get_WhenManyCandidatesExist_ReturnsAtMostFiftyEarliestFirst()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        // A month of free time across three rooms is roughly ninety candidates.
        using JsonDocument body = await SearchAsync(
            client,
            "durationMinutes=60&from=2026-09-15T00:00:00Z&to=2026-10-15T00:00:00Z");

        IReadOnlyList<DateTimeOffset> starts = body.RootElement.EnumerateArray()
            .Select(slot => slot.GetProperty("start").GetDateTimeOffset())
            .ToList();

        Assert.Equal(50, starts.Count);
        Assert.Equal(starts.OrderBy(start => start), starts);
    }

    [Fact]
    public async Task Get_ReturnsExactlyTheDocumentedFields()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        using JsonDocument body = await SearchAsync(client, $"durationMinutes=60&{Tuesday}");

        IReadOnlyList<string> fields = body.RootElement[0].EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["end", "roomId", "roomName", "start"], fields);
    }

    private static async Task<JsonDocument> SearchAsync(HttpClient client, string query)
    {
        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/availability?{query}", UriKind.Relative),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
    }

    private static async Task<HttpResponseMessage> RefusedAsync(string query)
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        return await client.GetAsync(new Uri($"/availability?{query}", UriKind.Relative), Token);
    }
}
