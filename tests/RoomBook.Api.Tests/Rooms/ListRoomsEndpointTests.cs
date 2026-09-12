using System.Net;
using System.Text.Json;
using RoomBook.Domain.Rooms;

namespace RoomBook.Api.Tests.Rooms;

public sealed class ListRoomsEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_WhenRoomsAreSeeded_ReturnsEveryRoomWithCamelCaseFields()
    {
        using RoomBookApplication application = new();
        using HttpClient client = application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/rooms", UriKind.Relative), Token);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement first = body.RootElement[0];
        Assert.Equal(3, body.RootElement.GetArrayLength());
        Assert.Equal("Ada", first.GetProperty("name").GetString());
        Assert.Equal(4, first.GetProperty("capacity").GetInt32());
        Assert.Equal("Europe/Istanbul", first.GetProperty("timeZone").GetString());
        Assert.Equal("09:00", first.GetProperty("opensAt").GetString());
        Assert.Equal("18:00", first.GetProperty("closesAt").GetString());
        Assert.NotEqual(Guid.Empty, first.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Get_WhenARoomKeepsItsOwnHours_ReturnsThoseHours()
    {
        using RoomBookApplication application = new();
        using HttpClient client = application.CreateClient();

        using JsonDocument body = JsonDocument.Parse(
            await client.GetStringAsync(new Uri("/rooms", UriKind.Relative), Token));
        JsonElement kapadokya = body.RootElement[2];

        Assert.Equal("Kapadokya", kapadokya.GetProperty("name").GetString());
        Assert.Equal("08:30", kapadokya.GetProperty("opensAt").GetString());
        Assert.Equal("17:30", kapadokya.GetProperty("closesAt").GetString());
    }

    [Fact]
    public async Task Get_WhenCalledTwice_ReturnsTheSameOrder()
    {
        using RoomBookApplication application = new();
        using HttpClient client = application.CreateClient();

        IReadOnlyList<string> first = await ReadNamesAsync(client);
        IReadOnlyList<string> second = await ReadNamesAsync(client);

        Assert.Equal(["Ada", "Boğaziçi", "Kapadokya"], first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Get_WhenThereAreNoRooms_Returns200AndAnEmptyArray()
    {
        using RoomBookApplication application = new(rooms: []);
        using HttpClient client = application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/rooms", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Get_FromTwoSeparateHosts_ReturnsTheSameIdentifiers()
    {
        // Two hosts stand in for two process lifetimes: a client may store an id and still find
        // the room after a restart, even though storage is in memory.
        using RoomBookApplication first = new();
        using RoomBookApplication second = new();
        using HttpClient firstClient = first.CreateClient();
        using HttpClient secondClient = second.CreateClient();

        IReadOnlyList<Guid> firstIds = await ReadIdsAsync(firstClient);
        IReadOnlyList<Guid> secondIds = await ReadIdsAsync(secondClient);

        Assert.Equal(firstIds, secondIds);
        Assert.Equal(3, firstIds.Distinct().Count());
    }

    private static async Task<IReadOnlyList<string>> ReadNamesAsync(HttpClient client)
    {
        using JsonDocument body = JsonDocument.Parse(
            await client.GetStringAsync(new Uri("/rooms", UriKind.Relative), Token));

        return body.RootElement.EnumerateArray()
            .Select(room => room.GetProperty("name").GetString() ?? string.Empty)
            .ToList();
    }

    private static async Task<IReadOnlyList<Guid>> ReadIdsAsync(HttpClient client)
    {
        using JsonDocument body = JsonDocument.Parse(
            await client.GetStringAsync(new Uri("/rooms", UriKind.Relative), Token));

        return body.RootElement.EnumerateArray()
            .Select(room => room.GetProperty("id").GetGuid())
            .ToList();
    }
}
