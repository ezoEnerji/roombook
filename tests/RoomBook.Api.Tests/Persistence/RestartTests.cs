using System.Net;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RoomBook.Api.Tests.Bookings;
using RoomBook.Domain.Shared;
using static RoomBook.Api.Tests.Bookings.BookingRequests;

namespace RoomBook.Api.Tests.Persistence;

/// <summary>
/// The only tests here that actually restart anything. Every other test uses an in-memory database,
/// which cannot answer the question this slice exists for — so these use a temp file and two hosts in
/// sequence, and delete the file afterwards.
/// </summary>
public sealed class RestartTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"roombook-{Guid.CreateVersion7():N}.db");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string ConnectionString => $"Data Source={_path}";

    [Fact]
    public async Task ABookingMadeBeforeARestart_IsStillThereAfterwards()
    {
        Uri location;
        string before;

        using (RoomBookApplication first = new(clock: Clock(), connectionString: ConnectionString))
        {
            using HttpClient client = first.CreateClient();
            HttpResponseMessage created = await PostAsync(client, Body(), Token);

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            location = created.Headers.Location!;
            before = await created.Content.ReadAsStringAsync(Token);
        }

        // A new host, a new connection, the same file: this is the restart.
        using RoomBookApplication second = new(clock: Clock(), connectionString: ConnectionString);
        using HttpClient after = second.CreateClient();

        HttpResponseMessage response = await after.GetAsync(location, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task AfterARestart_TheRoomIsStillOccupied()
    {
        // The rule outlives the process too, not only the row: the same window must still be refused.
        using (RoomBookApplication first = new(clock: Clock(), connectionString: ConnectionString))
        {
            using HttpClient client = first.CreateClient();
            await PostAsync(client, Body(), Token);
        }

        using RoomBookApplication second = new(clock: Clock(), connectionString: ConnectionString);
        using HttpClient after = second.CreateClient();

        HttpResponseMessage response = await PostAsync(after, Body(), Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.BookingOverlap, await CodeAsync(response, Token));
    }

    [Fact]
    public async Task StartingTwice_DoesNotDuplicateTheSeededRooms()
    {
        using (RoomBookApplication first = new(connectionString: ConnectionString))
        {
            using HttpClient client = first.CreateClient();
            await client.GetStringAsync(new Uri("/rooms", UriKind.Relative), Token);
        }

        using RoomBookApplication second = new(connectionString: ConnectionString);
        using HttpClient after = second.CreateClient();

        using JsonDocument rooms = JsonDocument.Parse(
            await after.GetStringAsync(new Uri("/rooms", UriKind.Relative), Token));

        Assert.Equal(3, rooms.RootElement.GetArrayLength());

        // And the identifiers a client may have stored are the same ones, because seeding is keyed
        // by them rather than generating new ones.
        Assert.Equal(
            [AdaId, BogaziciId, "0192a1b2-c3d4-7a03-8b03-000000000003"],
            rooms.RootElement.EnumerateArray()
                .Select(room => room.GetProperty("id").GetString()?.ToLowerInvariant())
                .OrderBy(id => id, StringComparer.Ordinal));
    }

    public void Dispose() => TempFiles.DeleteBestEffort(_path);
}
