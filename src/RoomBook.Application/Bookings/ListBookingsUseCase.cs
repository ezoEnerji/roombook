using RoomBook.Application.Rooms;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Application.Bookings;

/// <summary>
/// Lists the bookings touching a window. Ordered by start and then by room name, the same comparison
/// the room listing and the availability search use, so the three endpoints never disagree about
/// what "in order" means.
/// </summary>
public sealed class ListBookingsUseCase
{
    /// <summary>
    /// Documented in `docs/conventions.md`. Higher than the availability cap on purpose: suggestions
    /// are more useful when there are few of them, while a truncated list of facts hides reality, so
    /// this bound exists only to keep a response finite.
    /// </summary>
    public const int MaxBookings = 200;

    private readonly IRoomRepository _rooms;
    private readonly IBookingRepository _bookings;

    public ListBookingsUseCase(IRoomRepository rooms, IBookingRepository bookings)
    {
        _rooms = rooms;
        _bookings = bookings;
    }

    public async ValueTask<Result<IReadOnlyList<Booking>>> ExecuteAsync(
        ListBookingsQuery query,
        CancellationToken cancellationToken)
    {
        Result<TimeSlot> window = QueryWindow.Create(query.From, query.To);
        if (window.IsFailure)
        {
            return Result<IReadOnlyList<Booking>>.Failure(window.Error);
        }

        IReadOnlyList<Room> rooms;

        if (query.RoomId is Guid roomId)
        {
            // An unknown room is a mistake worth reporting, not an empty answer: the caller would
            // otherwise read "nothing is booked" from a typo in an identifier. The room found here is
            // also the only one whose name the ordering can need.
            Result<Room> room = await _rooms.FindAsync(roomId, cancellationToken);
            if (room.IsFailure)
            {
                return Result<IReadOnlyList<Booking>>.Failure(room.Error);
            }

            rooms = [room.Value];
        }
        else
        {
            rooms = await _rooms.GetAllAsync(cancellationToken);
        }

        IReadOnlyList<Booking> bookings = await _bookings.ListAsync(window.Value, query.RoomId, cancellationToken);
        Dictionary<Guid, string> namesById = rooms.ToDictionary(room => room.Id, room => room.Name);

        return Result<IReadOnlyList<Booking>>.Success(bookings
            .OrderBy(booking => booking.Slot.Start)
            .ThenBy(booking => namesById.GetValueOrDefault(booking.RoomId, string.Empty), StringComparer.Ordinal)
            .Take(MaxBookings)
            .ToList());
    }
}
