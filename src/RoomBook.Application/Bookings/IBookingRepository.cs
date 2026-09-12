using RoomBook.Domain.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Application.Bookings;

public interface IBookingRepository
{
    /// <summary>
    /// Stores the booking unless it overlaps an existing one in the same room, and does both as one
    /// indivisible step. BR-2 is an invariant, not a validation: checking first and writing second
    /// leaves a window in which two callers both pass the check and the room ends up double-booked.
    /// The in-memory adapter holds a lock; a database would use a constraint or a transaction.
    /// </summary>
    ValueTask<Result<Booking>> AddIfNoOverlapAsync(Booking booking, CancellationToken cancellationToken);

    /// <summary>
    /// The booking with this identifier, or a failure carrying <c>booking.not_found</c>.
    /// </summary>
    ValueTask<Result<Booking>> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The bookings in one room that touch a window. Scoped rather than "all bookings" so the
    /// availability search does not grow with the size of the store.
    /// </summary>
    ValueTask<IReadOnlyList<Booking>> ListForRoomAsync(
        Guid roomId,
        TimeSlot window,
        CancellationToken cancellationToken);
}
