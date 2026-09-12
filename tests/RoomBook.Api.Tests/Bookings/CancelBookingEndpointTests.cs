using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using RoomBook.Domain.Shared;
using static RoomBook.Api.Tests.Bookings.BookingRequests;

namespace RoomBook.Api.Tests.Bookings;

public sealed class CancelBookingEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Delete_WhenTheBookingHasNotStarted_Returns204()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();
        Uri booking = await CreateAsync(client);

        HttpResponseMessage response = await client.DeleteAsync(booking, Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);
    }

    [Fact]
    public async Task Delete_ThenBookingTheSameWindowAgain_Succeeds()
    {
        // The point of cancelling: the room is free again, immediately.
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();
        Uri booking = await CreateAsync(client);

        await client.DeleteAsync(booking, Token);
        HttpResponseMessage again = await PostAsync(client, Body(), Token);

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenReadingTheBooking_Returns404()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();
        Uri booking = await CreateAsync(client);

        await client.DeleteAsync(booking, Token);
        HttpResponseMessage read = await client.GetAsync(booking, Token);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(ErrorCodes.BookingNotFound, await CodeAsync(read, Token));
    }

    [Fact]
    public async Task Delete_WhenCalledTwice_TheSecondIsNotFound()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();
        Uri booking = await CreateAsync(client);

        await client.DeleteAsync(booking, Token);
        HttpResponseMessage second = await client.DeleteAsync(booking, Token);
        using JsonDocument document = JsonDocument.Parse(await second.Content.ReadAsStringAsync(Token));

        // The full problem shape, not only the code: a 404 is as much part of the error contract as
        // a 409, and the envelope is the part a client parses.
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ErrorCodes.BookingNotFound, document.RootElement.GetProperty("code").GetString());
        Assert.Equal(404, document.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Delete_WhenTheBookingNeverExisted_ReturnsTheSameAnswerAsCancellingTwice()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(
            new Uri($"/bookings/{Guid.CreateVersion7()}", UriKind.Relative),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.BookingNotFound, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Delete_OneMinuteBeforeTheStart_Returns204()
    {
        FakeTimeProvider clock = Clock();
        using RoomBookApplication application = new(clock: clock);
        using HttpClient client = application.CreateClient();
        Uri booking = await CreateAsync(client);

        // The booking starts at 07:00Z and the clock stands at 06:00Z.
        clock.Advance(TimeSpan.FromMinutes(59));

        HttpResponseMessage response = await client.DeleteAsync(booking, Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AtExactlyTheStart_Returns409()
    {
        FakeTimeProvider clock = Clock();
        using RoomBookApplication application = new(clock: clock);
        using HttpClient client = application.CreateClient();
        Uri booking = await CreateAsync(client);

        clock.Advance(TimeSpan.FromHours(1));

        HttpResponseMessage response = await client.DeleteAsync(booking, Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.CancelAfterStart, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Delete_WhileTheMeetingIsUnderWay_Returns409()
    {
        FakeTimeProvider clock = Clock();
        using RoomBookApplication application = new(clock: clock);
        using HttpClient client = application.CreateClient();
        Uri booking = await CreateAsync(client);

        clock.Advance(TimeSpan.FromMinutes(90));

        HttpResponseMessage response = await client.DeleteAsync(booking, Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.CancelAfterStart, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task Delete_WhenRefused_ReturnsAProblemDocumentWithItsCode()
    {
        FakeTimeProvider clock = Clock();
        using RoomBookApplication application = new(clock: clock);
        using HttpClient client = application.CreateClient();
        Uri booking = await CreateAsync(client);

        clock.Advance(TimeSpan.FromHours(2));

        HttpResponseMessage response = await client.DeleteAsync(booking, Token);
        string payload = await response.Content.ReadAsStringAsync(Token);
        using JsonDocument document = JsonDocument.Parse(payload);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ErrorCodes.CancelAfterStart, document.RootElement.GetProperty("code").GetString());
        Assert.Equal(409, document.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("StackTrace", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Uri> CreateAsync(HttpClient client)
    {
        HttpResponseMessage created = await PostAsync(client, Body(), Token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return created.Headers.Location!;
    }
}
