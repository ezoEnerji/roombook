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
    /// The bookings that touch a window, optionally narrowed to one room. Always scoped by time
    /// rather than "all bookings", so neither the availability search nor a listing grows with the
    /// size of the store.
    /// </summary>
    ValueTask<IReadOnlyList<Booking>> ListAsync(
        TimeSlot window,
        Guid? roomId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes the booking and returns it, or reports <c>booking.not_found</c> if it has already
    /// gone. This is the single arbiter of a cancellation race: two callers can both decide a
    /// booking is cancellable, and only one of them can remove it.
    /// </summary>
    ValueTask<Result<Booking>> RemoveAsync(Guid id, CancellationToken cancellationToken);
}
