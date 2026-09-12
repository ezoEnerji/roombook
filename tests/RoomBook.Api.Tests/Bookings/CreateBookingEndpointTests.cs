using System.Net;
using System.Text.Json;
using RoomBook.Domain.Shared;
using static RoomBook.Api.Tests.Bookings.BookingRequests;

namespace RoomBook.Api.Tests.Bookings;

public sealed class CreateBookingEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_WithAValidRequest_Returns201WithLocationAndBody()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage response = await PostAsync(client, Body(), Token);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Guid id = body.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal($"/bookings/{id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(AdaId, body.RootElement.GetProperty("roomId").GetString(), ignoreCase: true);
        Assert.Equal("Sprint review", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("Abdullah", body.RootElement.GetProperty("organizer").GetString());
        Assert.Equal(4, body.RootElement.GetProperty("attendeeCount").GetInt32());
        Assert.Equal(
            new DateTimeOffset(2026, 9, 14, 7, 0, 0, TimeSpan.Zero),
            body.RootElement.GetProperty("start").GetDateTimeOffset());
    }

    [Fact]
    public async Task Post_WhenOverlappingInTheSameRoom_Returns409()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        await PostAsync(client, Body(), Token);
        HttpResponseMessage second = await PostAsync(
            client,
            Body(start: "2026-09-14T07:30:00Z", end: "2026-09-14T08:30:00Z"),
            Token);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.BookingOverlap, await CodeAsync(second, Token));
    }

    [Fact]
    public async Task Post_WhenTheSameWindowIsBookedInAnotherRoom_Returns201()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        await PostAsync(client, Body(), Token);
        HttpResponseMessage second = await PostAsync(client, Body(roomId: BogaziciId), Token);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Theory]
    [InlineData("2026-09-14T08:00:00Z", "2026-09-14T09:00:00Z")]
    [InlineData("2026-09-14T06:30:00Z", "2026-09-14T07:00:00Z")]
    public async Task Post_WhenTouchingAnExistingBooking_Returns201(string start, string end)
    {
        // BR-3, both directions: starting exactly when the other ends, and ending exactly when it
        // starts. The existing booking runs 07:00–08:00Z.
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        await PostAsync(client, Body(), Token);
        HttpResponseMessage response = await PostAsync(client, Body(start: start, end: end), Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenTheWindowReachesPastClosingTime_Returns422()
    {
        HttpResponseMessage response = await RefusedAsync(
            Body(start: "2026-09-14T14:30:00Z", end: "2026-09-14T15:30:00Z"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.OutsideBusinessHours, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenTheDurationIsTooShort_Returns422()
    {
        HttpResponseMessage response = await RefusedAsync(
            Body(start: "2026-09-14T07:00:00Z", end: "2026-09-14T07:14:00Z"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.DurationOutOfRange, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenAttendeesExceedTheRoomsCapacity_Returns422()
    {
        // Ada holds four people.
        HttpResponseMessage response = await RefusedAsync(Body(attendeeCount: 5));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.AttendeesExceedCapacity, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenTheStartIsInThePast_Returns422()
    {
        HttpResponseMessage response = await RefusedAsync(
            Body(start: "2026-09-14T05:00:00Z", end: "2026-09-14T05:30:00Z"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.StartInPast, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenBeyondTheNinetyDayHorizon_Returns422()
    {
        HttpResponseMessage response = await RefusedAsync(
            Body(start: "2027-01-14T07:00:00Z", end: "2027-01-14T08:00:00Z"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.TooFarInFuture, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenTheRoomDoesNotExist_Returns404()
    {
        HttpResponseMessage response = await RefusedAsync(
            Body(roomId: "0192a1b2-c3d4-7aff-8bff-0000000000ff"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.RoomNotFound, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenSeveralRulesBreak_ReturnsTheOneTheDocumentedOrderSelects()
    {
        // In the past and six hours long: the order in the spec puts BR-8 before BR-4.
        HttpResponseMessage response = await RefusedAsync(
            Body(start: "2026-09-14T00:00:00Z", end: "2026-09-14T06:00:00Z"));

        Assert.Equal(ErrorCodes.StartInPast, await CodeAsync(response, Token));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"roomId":"0192a1b2-c3d4-7a01-8b01-000000000001","surprise":true}""")]
    [InlineData("""{"title":"Sprint review","organizer":"Abdullah"}""")]
    public async Task Post_WhenTheBodyIsMalformed_Returns400(string json)
    {
        HttpResponseMessage response = await RefusedAsync(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.RequestInvalid, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenTheTitleIsTooLong_Returns400()
    {
        HttpResponseMessage response = await RefusedAsync(Body(title: new string('a', 201)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.RequestInvalid, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenTheOrganizerIsTooLong_Returns400()
    {
        HttpResponseMessage response = await RefusedAsync(Body(organizer: new string('a', 101)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.RequestInvalid, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenTheTimestampsAreNotUtc_Returns400()
    {
        // BR-5: the wire carries UTC instants, not local times with an offset.
        HttpResponseMessage response = await RefusedAsync(
            Body(start: "2026-09-14T10:00:00+03:00", end: "2026-09-14T11:00:00+03:00"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.RequestInvalid, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenAttendeeCountIsZero_Returns400()
    {
        HttpResponseMessage response = await RefusedAsync(Body(attendeeCount: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.RequestInvalid, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenTheBodyIsOverTheLimit_Returns413()
    {
        // docs/security.md caps request bodies at 32 KB. This gets its own status precisely so the
        // test proves the limit: an oversized field would also produce a 400, which would look the
        // same whether or not the limit existed.
        HttpResponseMessage response = await RefusedAsync(Body(title: new string('a', 40 * 1024)));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(ErrorCodes.RequestTooLarge, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenTheBodyIsUnderTheLimit_IsRefusedForItsContentRatherThanItsSize()
    {
        // Just under 32 KB: the limit must not fire, so this comes back as an ordinary field-limit
        // refusal. Without this, an over-eager limit would look like a passing test above.
        HttpResponseMessage response = await RefusedAsync(Body(title: new string('a', 30 * 1024)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.RequestInvalid, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenTooLongAndOutsideBusinessHours_Returns422WithDurationCode()
    {
        // The documented order puts BR-4 before BR-1, and this proves it over HTTP as well as in
        // the domain: tomorrow at 06:00 local is before opening, and six hours is over the limit.
        HttpResponseMessage response = await RefusedAsync(
            Body(start: "2026-09-15T03:00:00Z", end: "2026-09-15T09:00:00Z"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.DurationOutOfRange, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Post_WhenRefused_ReturnsAProblemDocumentThatLeaksNothing()
    {
        HttpResponseMessage response = await RefusedAsync(Body(attendeeCount: 5));
        string payload = await response.Content.ReadAsStringAsync(Token);
        using JsonDocument document = JsonDocument.Parse(payload);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ErrorCodes.AttendeesExceedCapacity, document.RootElement.GetProperty("code").GetString());
        Assert.Equal(422, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("detail").GetString()));
        Assert.DoesNotContain("StackTrace", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_WhenTwoIdenticalRequestsRace_OnlyOneSucceeds()
    {
        // BR-2 is an invariant, not a validation: a check followed by a separate write would let
        // both of these through. Sending them sequentially would prove nothing, so they go together.
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient first = application.CreateClient();
        using HttpClient second = application.CreateClient();

        HttpResponseMessage[] responses = await Task.WhenAll(
            PostAsync(first, Body(), Token),
            PostAsync(second, Body(), Token));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));

        HttpResponseMessage conflict = Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(ErrorCodes.BookingOverlap, await CodeAsync(conflict, Token));

        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }
    }

    private static async Task<HttpResponseMessage> RefusedAsync(string json)
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        return await PostAsync(client, json, Token);
    }
}
