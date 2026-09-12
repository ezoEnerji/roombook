using RoomBook.Domain.Rooms;

namespace RoomBook.Application.Rooms;

/// <summary>
/// The storage port for rooms. V1 is backed by an in-memory adapter (ADR-0001); the port is
/// asynchronous because the database that replaces it will be, and changing the signature later
/// would ripple through every use case.
/// </summary>
public interface IRoomRepository
{
    ValueTask<IReadOnlyList<Room>> GetAllAsync(CancellationToken cancellationToken);
}
