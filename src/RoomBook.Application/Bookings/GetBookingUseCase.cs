using RoomBook.Domain.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Application.Bookings;

public sealed class GetBookingUseCase
{
    private readonly IBookingRepository _bookings;

    public GetBookingUseCase(IBookingRepository bookings)
    {
        _bookings = bookings;
    }

    public ValueTask<Result<Booking>> ExecuteAsync(Guid id, CancellationToken cancellationToken) =>
        _bookings.FindAsync(id, cancellationToken);
}
