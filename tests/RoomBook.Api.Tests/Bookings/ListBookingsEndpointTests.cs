using System.Net;
using System.Text.Json;
using RoomBook.Domain.Shared;
using static RoomBook.Api.Tests.Bookings.BookingRequests;

namespace RoomBook.Api.Tests.Bookings;

public sealed class ListBookingsEndpointTests
{
    private const string Tuesday = "from=2026-09-15T00:00:00Z&to=2026-09-15T23:00:00Z";

    private const string KapadokyaId = "0192a1b2-c3d4-7a03-8b03-000000000003";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A booking inside the window these tests query. The shared request helper defaults to Monday,
    /// which is a day earlier — a default that quietly emptied three of these tests before the
    /// assertions caught it.
    /// </summary>
    private static string TuesdayBooking(string roomId, string start = "07:00", string end = "08:00") =>
        Body(roomId: roomId, start: $"2026-09-15T{start}:00Z", end: $"2026-09-15T{end}:00Z");

    [Fact]
    public async Task Get_OrdersByStartThenRoomName()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        // Kapadokya at 06:00Z, then two rooms sharing 07:00Z, which the room name breaks ordinally.
        await PostAsync(client, TuesdayBooking(KapadokyaId, "06:00", "07:00"), Token);
        await PostAsync(client, TuesdayBooking(BogaziciId), Token);
        await PostAsync(client, TuesdayBooking(AdaId), Token);

        using JsonDocument body = await ListAsync(client, Tuesday);

        Assert.Equal(
            [KapadokyaId, AdaId, BogaziciId],
            body.RootElement.EnumerateArray().Select(booking => booking.GetProperty("roomId").GetString()?.ToLowerInvariant()));
    }

    [Fact]
    public async Task Get_WhenNarrowedToOneRoom_ReturnsThatRoomOnly()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        await PostAsync(client, TuesdayBooking(AdaId), Token);
        await PostAsync(client, TuesdayBooking(BogaziciId), Token);

        using JsonDocument body = await ListAsync(client, $"{Tuesday}&roomId={AdaId}");

        JsonElement only = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal(AdaId, only.GetProperty("roomId").GetString(), ignoreCase: true);
    }

    [Fact]
    public async Task Get_WhenNothingIsBooked_Returns200AndAnEmptyList()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/bookings?{Tuesday}", UriKind.Relative),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Get_WhenTheBookingIsOutsideTheWindow_DoesNotIncludeIt()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        await PostAsync(client, Body(start: "2026-09-16T07:00:00Z", end: "2026-09-16T08:00:00Z"), Token);

        using JsonDocument body = await ListAsync(client, Tuesday);

        Assert.Empty(body.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Get_AfterCancelling_DoesNotIncludeTheBooking()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage created = await PostAsync(client, TuesdayBooking(AdaId), Token);

        // Present before the cancellation, so the assertion below cannot pass by accident.
        using (JsonDocument before = await ListAsync(client, Tuesday))
        {
            Assert.Single(before.RootElement.EnumerateArray());
        }

        await client.DeleteAsync(created.Headers.Location!, Token);

        using JsonDocument body = await ListAsync(client, Tuesday);

        Assert.Empty(body.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Get_WhenTheRoomDoesNotExist_Returns404()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/bookings?{Tuesday}&roomId=0192a1b2-c3d4-7aff-8bff-0000000000ff", UriKind.Relative),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.RoomNotFound, await CodeAsync(response, Token));
    }

    [Theory]
    [InlineData("to=2026-09-15T23:00:00Z")]
    [InlineData("from=2026-09-15T00:00:00Z")]
    [InlineData("from=2026-09-15T23:00:00Z&to=2026-09-15T00:00:00Z")]
    [InlineData("from=2026-09-15T00:00:00Z&to=2026-10-20T00:00:00Z")]
    [InlineData("from=2026-09-15T03:00:00%2B03:00&to=2026-09-15T23:00:00Z")]
    [InlineData("from=2026-09-15T00:00:00Z&to=2026-09-15T23:00:00Z&roomId=not-a-guid")]
    public async Task Get_WhenTheQueryIsMalformed_Returns400(string query)
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri($"/bookings?{query}", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.RequestInvalid, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Get_ReturnsExactlyTheDocumentedFields()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        await PostAsync(client, TuesdayBooking(AdaId), Token);

        using JsonDocument body = await ListAsync(client, Tuesday);

        IReadOnlyList<string> fields = body.RootElement[0].EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            ["attendeeCount", "end", "id", "organizer", "roomId", "start", "title"],
            fields);
    }

    private static async Task<JsonDocument> ListAsync(HttpClient client, string query)
    {
        HttpResponseMessage response = await client.GetAsync(new Uri($"/bookings?{query}", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
    }
}
