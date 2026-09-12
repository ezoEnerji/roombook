using RoomBook.Application.Bookings;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Api.Infrastructure;

/// <summary>
/// The V1 booking store. The lock is not decoration: BR-2 says a room never holds two overlapping
/// bookings, and that only holds if checking and inserting happen as one step. A database adapter
/// would move the same guarantee into a constraint or a transaction.
/// </summary>
public sealed class InMemoryBookingRepository : IBookingRepository
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, Booking> _bookings = [];

    public ValueTask<Result<Booking>> AddIfNoOverlapAsync(Booking booking, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            foreach (Booking existing in _bookings.Values)
            {
                if (existing.RoomId == booking.RoomId && existing.Slot.Overlaps(booking.Slot))
                {
                    return ValueTask.FromResult(Result<Booking>.Failure(
                        ErrorCodes.BookingOverlap,
                        "The room is already booked for part of that window."));
                }
            }

            _bookings.Add(booking.Id, booking);

            return ValueTask.FromResult(Result<Booking>.Success(booking));
        }
    }

    public ValueTask<IReadOnlyList<Booking>> ListAsync(
        TimeSlot window,
        Guid? roomId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<Booking> touching = _bookings.Values
                .Where(booking => roomId is null || booking.RoomId == roomId)
                .Where(booking => booking.Slot.Overlaps(window))
                .ToList();

            return ValueTask.FromResult(touching);
        }
    }

    public ValueTask<Result<Booking>> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_bookings.Remove(id, out Booking? removed))
            {
                return ValueTask.FromResult(Result<Booking>.Failure(
                    ErrorCodes.BookingNotFound,
                    "There is no booking with that identifier."));
            }

            return ValueTask.FromResult(Result<Booking>.Success(removed));
        }
    }

    public ValueTask<Result<Booking>> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_bookings.TryGetValue(id, out Booking? booking)
                ? Result<Booking>.Success(booking)
                : Result<Booking>.Failure(ErrorCodes.BookingNotFound, "There is no booking with that identifier."));
        }
    }
}
