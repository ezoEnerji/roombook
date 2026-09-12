using System.Net;
using System.Text.Json;

namespace RoomBook.Api.Tests.Rooms;

public sealed class ListRoomsEndpointTests
{
    /// <summary>
    /// The seeded rooms exactly as a client sees them, identifiers included. The identifiers are a
    /// published contract: a client may store one, so changing a value here is a breaking change
    /// and this table is what makes that visible.
    /// </summary>
    public static TheoryData<string, string, int, string, string> SeededRooms => new()
    {
        { "Ada", "0192a1b2-c3d4-7a01-8b01-000000000001", 4, "09:00", "18:00" },
        { "Boğaziçi", "0192a1b2-c3d4-7a02-8b02-000000000002", 12, "09:00", "18:00" },
        { "Kapadokya", "0192a1b2-c3d4-7a03-8b03-000000000003", 24, "08:30", "17:30" },
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(SeededRooms))]
    public async Task Get_ReturnsEachSeededRoom_WithItsDocumentedValues(
        string name,
        string id,
        int capacity,
        string opensAt,
        string closesAt)
    {
        using RoomBookApplication application = new();
        using HttpClient client = application.CreateClient();

        using JsonDocument body = JsonDocument.Parse(
            await client.GetStringAsync(new Uri("/rooms", UriKind.Relative), Token));

        JsonElement room = body.RootElement.EnumerateArray()
            .Single(candidate => candidate.GetProperty("name").GetString() == name);

        Assert.Equal(Guid.Parse(id), room.GetProperty("id").GetGuid());
        Assert.Equal(capacity, room.GetProperty("capacity").GetInt32());
        Assert.Equal("Europe/Istanbul", room.GetProperty("timeZone").GetString());
        Assert.Equal(opensAt, room.GetProperty("opensAt").GetString());
        Assert.Equal(closesAt, room.GetProperty("closesAt").GetString());
    }

    [Fact]
    public async Task Get_ReturnsAllThreeRooms_WithStatus200()
    {
        using RoomBookApplication application = new();
        using HttpClient client = application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/rooms", UriKind.Relative), Token);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, body.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Get_ReturnsExactlyTheDocumentedFields()
    {
        // This is the behavioural half of FD-5: the architecture rule inspects contract types by
        // name and cannot see through a minimal-API lambda, so if an endpoint ever returned a
        // domain object directly, only the property set would give it away.
        using RoomBookApplication application = new();
        using HttpClient client = application.CreateClient();

        using JsonDocument body = JsonDocument.Parse(
            await client.GetStringAsync(new Uri("/rooms", UriKind.Relative), Token));

        IReadOnlyList<string> fields = body.RootElement[0].EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["capacity", "closesAt", "id", "name", "opensAt", "timeZone"], fields);
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
        using RoomBookApplication application = new();
        using HttpClient client = application.CreateClient();

        // An installation with no rooms is now made rather than injected: the store seeds them at
        // startup, so emptying it is the honest way to reach the state this asserts.
        await application.EmptyTheRoomsAsync(Token);

        HttpResponseMessage response = await client.GetAsync(new Uri("/rooms", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Get_FromTwoHosts_ReturnsTheSameIdentifiers()
    {
        // Two hosts in one process share the static seed, so this does not simulate a restart —
        // stability across restarts is guaranteed by the literal identifiers asserted above. What
        // this does prove is that nothing per-host (a factory, a request) invents an identifier.
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
