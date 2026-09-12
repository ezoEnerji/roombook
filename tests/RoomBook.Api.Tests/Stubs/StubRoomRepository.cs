using RoomBook.Application.Rooms;
using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Api.Tests.Stubs;

/// <summary>A fixed set of rooms, for the use-case tests that do not need a host.</summary>
internal sealed class StubRoomRepository : IRoomRepository
{
    private readonly IReadOnlyList<Room> _rooms;

    public StubRoomRepository(IReadOnlyList<Room> rooms)
    {
        _rooms = rooms;
    }

    public ValueTask<IReadOnlyList<Room>> GetAllAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_rooms);

    public ValueTask<Result<Room>> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        Room? room = _rooms.FirstOrDefault(candidate => candidate.Id == id);

        return ValueTask.FromResult(room is null
            ? Result<Room>.Failure(ErrorCodes.RoomNotFound, "There is no room with that identifier.")
            : Result<Room>.Success(room));
    }
}
