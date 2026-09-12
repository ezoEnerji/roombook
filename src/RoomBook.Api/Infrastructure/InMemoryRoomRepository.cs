using RoomBook.Application.Rooms;
using RoomBook.Domain.Rooms;

namespace RoomBook.Api.Infrastructure;

/// <summary>
/// The V1 storage adapter: rooms live in memory and nothing survives a restart (ADR-0001). It takes
/// its contents as a constructor argument rather than seeding itself, which is what lets a test
/// build the application with no rooms at all.
/// </summary>
public sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly IReadOnlyList<Room> _rooms;

    public InMemoryRoomRepository(IEnumerable<Room> rooms)
    {
        _rooms = rooms.ToList();
    }

    public ValueTask<IReadOnlyList<Room>> GetAllAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_rooms);
}
