using Microsoft.Data.Sqlite;
using RoomBook.Application.Rooms;
using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Infrastructure;

/// <summary>
/// Rooms out of SQLite. A row that cannot become a valid <see cref="Room"/> throws rather than being
/// quietly repaired: the rules that reject it are the same ones that accepted it on the way in, so a
/// row that fails them means the database has been edited by hand or corrupted.
/// </summary>
public sealed class SqliteRoomRepository : IRoomRepository
{
    private const string Columns = "id, name, capacity, time_zone_id, opens_at_minutes, closes_at_minutes";

    private readonly RoomBookStore _store;

    public SqliteRoomRepository(RoomBookStore store)
    {
        _store = store;
    }

    public async ValueTask<IReadOnlyList<Room>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = _store.Connect();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM rooms;";

        List<Room> rooms = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rooms.Add(Read(reader));
        }

        return rooms;
    }

    public async ValueTask<Result<Room>> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = _store.Connect();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM rooms WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id.ToString());

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? Result<Room>.Success(Read(reader))
            : Result<Room>.Failure(ErrorCodes.RoomNotFound, "There is no room with that identifier.");
    }

    private static Room Read(SqliteDataReader reader)
    {
        BusinessHours hours = BusinessHours.Create(
            RoomBookStore.FromMinutes(reader.GetInt64(4)),
            RoomBookStore.FromMinutes(reader.GetInt64(5))).Value;

        return Room.Create(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3),
            hours).Value;
    }
}
