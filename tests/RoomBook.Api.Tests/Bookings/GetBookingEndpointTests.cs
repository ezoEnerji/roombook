using System.Net;
using System.Text.Json;
using RoomBook.Domain.Shared;
using static RoomBook.Api.Tests.Bookings.BookingRequests;

namespace RoomBook.Api.Tests.Bookings;

public sealed class GetBookingEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_AfterCreating_ReturnsTheStoredBooking()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage created = await PostAsync(client, Body(), Token);
        Uri location = created.Headers.Location!;

        HttpResponseMessage response = await client.GetAsync(location, Token);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sprint review", body.RootElement.GetProperty("title").GetString());
        Assert.Equal(AdaId, body.RootElement.GetProperty("roomId").GetString(), ignoreCase: true);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 14, 8, 0, 0, TimeSpan.Zero),
            body.RootElement.GetProperty("end").GetDateTimeOffset());
    }

    [Fact]
    public async Task Get_ReturnsExactlyTheDocumentedFields()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage created = await PostAsync(client, Body(), Token);
        using JsonDocument body = JsonDocument.Parse(
            await (await client.GetAsync(created.Headers.Location!, Token)).Content.ReadAsStringAsync(Token));

        IReadOnlyList<string> fields = body.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // The behavioural half of FD-5 again: returning the domain type would change these names.
        Assert.Equal(
            ["attendeeCount", "end", "id", "organizer", "roomId", "start", "title"],
            fields);
    }

    [Fact]
    public async Task Get_WhenTheBookingDoesNotExist_Returns404()
    {
        using RoomBookApplication application = new(clock: Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/bookings/{Guid.CreateVersion7()}", UriKind.Relative),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.BookingNotFound, await CodeAsync(response, Token));
    }
}
