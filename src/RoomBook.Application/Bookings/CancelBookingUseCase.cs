using RoomBook.Domain.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Application.Bookings;

/// <summary>
/// Cancels a booking: find it, let the domain judge BR-9, then remove it. The removal is the arbiter
/// of a race — two callers can both find a cancellable booking, and the one that loses the removal
/// is told the booking is gone, which is the truth.
/// </summary>
public sealed class CancelBookingUseCase
{
    private readonly IBookingRepository _bookings;
    private readonly TimeProvider _clock;

    public CancelBookingUseCase(IBookingRepository bookings, TimeProvider clock)
    {
        _bookings = bookings;
        _clock = clock;
    }

    public async ValueTask<Result<Booking>> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        Result<Booking> booking = await _bookings.FindAsync(id, cancellationToken);
        if (booking.IsFailure)
        {
            return booking;
        }

        Result<Booking> cancellable = booking.Value.EnsureCancellableAt(_clock.GetUtcNow());
        if (cancellable.IsFailure)
        {
            return cancellable;
        }

        return await _bookings.RemoveAsync(id, cancellationToken);
    }
}
