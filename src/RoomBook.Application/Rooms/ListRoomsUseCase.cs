using RoomBook.Domain.Rooms;

namespace RoomBook.Application.Rooms;

/// <summary>
/// Lists the bookable rooms. Ordering is by name with ordinal comparison, so the sequence is
/// identical on every machine — culture-aware sorting would place Turkish characters differently
/// depending on where the process runs.
/// </summary>
public sealed class ListRoomsUseCase
{
    private readonly IRoomRepository _rooms;

    public ListRoomsUseCase(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public async ValueTask<IReadOnlyList<Room>> ExecuteAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Room> rooms = await _rooms.GetAllAsync(cancellationToken);

        return rooms.OrderBy(room => room.Name, StringComparer.Ordinal).ToList();
    }
}
