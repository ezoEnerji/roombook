using RoomBook.Application.Bookings;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Api.Tests.Stubs;

/// <summary>
/// A fixed set of bookings that answers the one question the read-only use cases ask. The write
/// operations throw rather than pretending: a use case that starts writing should fail loudly in a
/// test built on the assumption that it does not.
/// </summary>
internal sealed class StubBookingRepository : IBookingRepository
{
    private readonly IReadOnlyList<Booking> _bookings;

    public StubBookingRepository(IReadOnlyList<Booking> bookings)
    {
        _bookings = bookings;
    }

    public ValueTask<IReadOnlyList<Booking>> ListAsync(
        TimeSlot window,
        Guid? roomId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Booking> touching = _bookings
            .Where(booking => roomId is null || booking.RoomId == roomId)
            .Where(booking => booking.Slot.Overlaps(window))
            .ToList();

        return ValueTask.FromResult(touching);
    }

    public ValueTask<Result<Booking>> AddIfNoOverlapAsync(Booking booking, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This stub serves read-only use cases.");

    public ValueTask<Result<Booking>> FindAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This stub serves read-only use cases.");

    public ValueTask<Result<Booking>> RemoveAsync(
        Guid id,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This stub serves read-only use cases.");
}
