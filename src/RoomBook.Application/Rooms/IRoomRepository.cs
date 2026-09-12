using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Application.Rooms;

/// <summary>
/// The storage port for rooms. V1 is backed by an in-memory adapter (ADR-0001); the port is
/// asynchronous because the database that replaces it will be, and changing the signature later
/// would ripple through every use case.
/// </summary>
public interface IRoomRepository
{
    ValueTask<IReadOnlyList<Room>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The room with this identifier, or a failure carrying <c>room.not_found</c>. Absence is an
    /// outcome the caller must handle, so it is a result rather than a null.
    /// </summary>
    ValueTask<Result<Room>> FindAsync(Guid id, CancellationToken cancellationToken);
}
