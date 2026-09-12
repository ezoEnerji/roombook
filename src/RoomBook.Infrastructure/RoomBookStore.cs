using Microsoft.Data.Sqlite;
using RoomBook.Domain.Rooms;

namespace RoomBook.Infrastructure;

/// <summary>
/// Owns the SQLite database: its schema, its seeded rooms, and the connections the repositories use.
/// <para>
/// Instants are stored as UTC ticks and opening hours as minutes from midnight. Neither ever reaches
/// the wire, and both compare exactly — storing times as text would work only while every value
/// happened to be the same fixed-width UTC format, which is a bug waiting for the first exception.
/// </para>
/// </summary>
public sealed class RoomBookStore : IDisposable
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS rooms (
            id                TEXT    PRIMARY KEY,
            name              TEXT    NOT NULL,
            capacity          INTEGER NOT NULL,
            time_zone_id      TEXT    NOT NULL,
            opens_at_minutes  INTEGER NOT NULL,
            closes_at_minutes INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS bookings (
            id             TEXT    PRIMARY KEY,
            room_id        TEXT    NOT NULL,
            title          TEXT    NOT NULL,
            organizer      TEXT    NOT NULL,
            start_ticks    INTEGER NOT NULL,
            end_ticks      INTEGER NOT NULL,
            attendee_count INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_bookings_room_window
            ON bookings (room_id, start_ticks, end_ticks);
        """;

    private readonly string _connectionString;

    /// <summary>
    /// An in-memory database exists only while a connection to it is open, so one is held for as long
    /// as the store lives. For a file database it costs nothing and keeps the code uniform.
    /// </summary>
    private readonly SqliteConnection _keepAlive;

    public RoomBookStore(string connectionString)
    {
        _connectionString = connectionString;
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();
    }

    /// <summary>
    /// Creates the schema if it is absent and makes sure the seeded rooms are present. Safe to call on
    /// every start: the rooms are inserted by their published identifiers and ignored if already there,
    /// so restarting neither duplicates them nor changes an identifier a client has stored.
    /// </summary>
    public void EnsureCreated()
    {
        using SqliteConnection connection = Connect();
        using SqliteTransaction transaction = connection.BeginTransaction();

        Execute(connection, transaction, Schema);

        foreach (Room room in SeedRooms.All)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO rooms
                    (id, name, capacity, time_zone_id, opens_at_minutes, closes_at_minutes)
                VALUES (@id, @name, @capacity, @timeZoneId, @opensAt, @closesAt);
                """;
            command.Parameters.AddWithValue("@id", room.Id.ToString());
            command.Parameters.AddWithValue("@name", room.Name);
            command.Parameters.AddWithValue("@capacity", room.Capacity);
            command.Parameters.AddWithValue("@timeZoneId", room.TimeZoneId);
            command.Parameters.AddWithValue("@opensAt", Minutes(room.Hours.Open));
            command.Parameters.AddWithValue("@closesAt", Minutes(room.Hours.Close));
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public SqliteConnection Connect()
    {
        SqliteConnection connection = new(_connectionString);
        connection.Open();

        // Writers serialise in SQLite; waiting briefly is the difference between a queue and an error.
        Execute(connection, transaction: null, "PRAGMA busy_timeout = 5000;");

        return connection;
    }

    public void Dispose() => _keepAlive.Dispose();

    internal static int Minutes(TimeOnly time) => (time.Hour * 60) + time.Minute;

    internal static TimeOnly FromMinutes(long minutes) => new((int)(minutes / 60), (int)(minutes % 60));

    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
