using Microsoft.Data.Sqlite;
using RoomBook.Application.Bookings;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Infrastructure;

/// <summary>
/// Bookings in SQLite. The two invariants that cannot be judged from a request alone are enforced by
/// conditional writes, so the decision and the write are one step:
/// <list type="bullet">
/// <item>BR-2 — the insert happens only if nothing in that room overlaps.</item>
/// <item>BR-9 — the delete happens only if the booking has not started yet.</item>
/// </list>
/// The rules still live in the domain; these conditions exist so no clock can overtake the answer
/// between deciding and writing. That is the gap `docs/architecture.md` recorded when the store was
/// a dictionary guarded by a lock.
/// </summary>
public sealed class SqliteBookingRepository : IBookingRepository
{
    private const string Columns = "id, room_id, title, organizer, start_ticks, end_ticks, attendee_count";

    private readonly RoomBookStore _store;

    public SqliteBookingRepository(RoomBookStore store)
    {
        _store = store;
    }

    public async ValueTask<Result<Booking>> AddIfNoOverlapAsync(
        Booking booking,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = _store.Connect();
        await using SqliteCommand command = connection.CreateCommand();

        // Half-open intervals (BR-3): touching endpoints do not overlap, which is why the comparisons
        // are strict on both sides.
        command.CommandText = $"""
            INSERT INTO bookings ({Columns})
            SELECT @id, @roomId, @title, @organizer, @start, @end, @attendees
            WHERE NOT EXISTS (
                SELECT 1 FROM bookings
                WHERE room_id = @roomId
                  AND start_ticks < @end
                  AND @start < end_ticks
            );
            """;
        command.Parameters.AddWithValue("@id", booking.Id.ToString());
        command.Parameters.AddWithValue("@roomId", booking.RoomId.ToString());
        command.Parameters.AddWithValue("@title", booking.Title);
        command.Parameters.AddWithValue("@organizer", booking.Organizer);
        command.Parameters.AddWithValue("@start", booking.Slot.Start.UtcTicks);
        command.Parameters.AddWithValue("@end", booking.Slot.End.UtcTicks);
        command.Parameters.AddWithValue("@attendees", booking.AttendeeCount);

        int stored = await command.ExecuteNonQueryAsync(cancellationToken);

        return stored == 1
            ? Result<Booking>.Success(booking)
            : Result<Booking>.Failure(
                ErrorCodes.BookingOverlap,
                "The room is already booked for part of that window.");
    }

    public async ValueTask<Result<Booking>> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = _store.Connect();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM bookings WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id.ToString());

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? Result<Booking>.Success(Read(reader))
            : Result<Booking>.Failure(ErrorCodes.BookingNotFound, "There is no booking with that identifier.");
    }

    public async ValueTask<IReadOnlyList<Booking>> ListAsync(
        TimeSlot window,
        Guid? roomId,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = _store.Connect();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns} FROM bookings
            WHERE (@roomId IS NULL OR room_id = @roomId)
              AND start_ticks < @end
              AND @start < end_ticks;
            """;
        command.Parameters.AddWithValue("@roomId", roomId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@start", window.Start.UtcTicks);
        command.Parameters.AddWithValue("@end", window.End.UtcTicks);

        List<Booking> bookings = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            bookings.Add(Read(reader));
        }

        return bookings;
    }

    public async ValueTask<Result<Booking>> RemoveAsync(
        Guid id,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = _store.Connect();
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        Booking? booking = await ReadOneAsync(connection, transaction, id, cancellationToken);
        if (booking is null)
        {
            await transaction.CommitAsync(cancellationToken);

            return Result<Booking>.Failure(ErrorCodes.BookingNotFound, "There is no booking with that identifier.");
        }

        await using SqliteCommand delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM bookings WHERE id = @id AND start_ticks > @now;";
        delete.Parameters.AddWithValue("@id", id.ToString());
        delete.Parameters.AddWithValue("@now", nowUtc.UtcTicks);

        int removed = await delete.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (removed == 1)
        {
            return Result<Booking>.Success(booking);
        }

        // The row was there a moment ago and the condition refused it, so the booking has started.
        // Reporting "not found" here would be cheaper and untrue.
        return Result<Booking>.Failure(
            ErrorCodes.CancelAfterStart,
            "A booking can only be cancelled before it starts.");
    }

    private static async Task<Booking?> ReadOneAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {Columns} FROM bookings WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id.ToString());

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    private static Booking Read(SqliteDataReader reader)
    {
        TimeSlot slot = TimeSlot.Create(
            new DateTimeOffset(reader.GetInt64(4), TimeSpan.Zero),
            new DateTimeOffset(reader.GetInt64(5), TimeSpan.Zero)).Value;

        return Booking.Rehydrate(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            slot,
            reader.GetInt32(6));
    }
}
