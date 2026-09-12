using System.Net;
using System.Text;
using System.Text.Json;
using RoomBook.Api.Tests.Bookings;
using static RoomBook.Api.Tests.Bookings.BookingRequests;

namespace RoomBook.Api.Tests.Problems;

/// <summary>
/// Every refusal is one document shape, whatever the status. Found by running the application for
/// real: a 413 came back without the `type` member every other refusal carries, because
/// <c>Results.Problem</c> fills that member from a framework table which has no entry for 413.
/// This is the regression guard for that.
/// </summary>
public sealed class ProblemDocumentShapeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryRefusal_CarriesTheSameMembers()
    {
        using RoomBookApplication application = new(clock: BookingRequests.Clock());
        using HttpClient client = application.CreateClient();

        IReadOnlyList<(string Case, HttpResponseMessage Response)> refusals =
        [
            ("malformed body (400)", await PostAsync(client, "not json", Token)),
            ("oversized body (413)", await PostAsync(client, Body(title: new string('a', 40 * 1024)), Token)),
            ("duration out of range (422)", await PostAsync(client, Body(end: "2026-09-14T13:00:00Z"), Token)),
            ("unknown booking (404)", await client.GetAsync(
                new Uri($"/bookings/{Guid.CreateVersion7()}", UriKind.Relative), Token)),
            ("overlap (409)", await OverlapAsync(client)),
        ];

        foreach ((string description, HttpResponseMessage response) in refusals)
        {
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

            IReadOnlyList<string> members = document.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(
                ["code", "detail", "status", "title", "type"],
                members);

            response.Dispose();
        }
    }

    [Fact]
    public async Task AnOversizedBody_CarriesATypeLikeEveryOtherRefusal()
    {
        // The narrow reproduction: 413 was the status whose `type` the framework table does not know.
        using RoomBookApplication application = new(clock: BookingRequests.Clock());
        using HttpClient client = application.CreateClient();

        HttpResponseMessage response = await PostAsync(client, Body(title: new string('a', 40 * 1024)), Token);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.True(
            document.RootElement.TryGetProperty("type", out JsonElement type),
            "A 413 problem document must carry `type`, like every other refusal.");
        Assert.False(string.IsNullOrWhiteSpace(type.GetString()));
    }

    private static async Task<HttpResponseMessage> OverlapAsync(HttpClient client)
    {
        await PostAsync(client, Body(), Token);

        return await PostAsync(client, Body(), Token);
    }
}
