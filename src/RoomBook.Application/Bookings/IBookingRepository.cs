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
    /// Removes the booking and returns it, unless the booking has already gone
    /// (<c>booking.not_found</c>) or has already started (<c>booking.cancel_after_start</c>).
    /// <para>
    /// The instant is a parameter because the decision and the write must be one step: a store that
    /// judges BR-9 itself, against the instant the caller read, cannot be overtaken by the clock
    /// between deciding and writing. The domain still owns the rule — this is the only place the
    /// port needed to learn about time, and it is the amendment S-005 made to ADR-0001's claim.
    /// </para>
    /// </summary>
    ValueTask<Result<Booking>> RemoveAsync(Guid id, DateTimeOffset nowUtc, CancellationToken cancellationToken);
}
