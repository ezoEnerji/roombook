using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Domain.Tests.Bookings;

/// <summary>
/// BR-9 at its boundary. "Now" is a value here, so the edge is pinned exactly rather than
/// approached with a tolerance.
/// </summary>
public sealed class BookingCancellationTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 15, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EnsureCancellableAt_OneMinuteBeforeTheStart_Succeeds()
    {
        Result<Booking> result = BookingStartingAtSeven().EnsureCancellableAt(Start.AddMinutes(-1));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EnsureCancellableAt_AtExactlyTheStart_ReturnsCancelAfterStart()
    {
        // BR-9 refuses at the start, not merely after it: a meeting that is beginning is under way.
        Result<Booking> result = BookingStartingAtSeven().EnsureCancellableAt(Start);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.CancelAfterStart, result.Error.Code);
    }

    [Fact]
    public void EnsureCancellableAt_AfterTheStart_ReturnsCancelAfterStart()
    {
        Result<Booking> result = BookingStartingAtSeven().EnsureCancellableAt(Start.AddMinutes(30));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.CancelAfterStart, result.Error.Code);
    }

    [Fact]
    public void EnsureCancellableAt_AfterTheBookingHasEnded_ReturnsCancelAfterStart()
    {
        // Still BR-9 rather than a different rule: the booking happened, and cancelling it would be
        // rewriting the past.
        Result<Booking> result = BookingStartingAtSeven().EnsureCancellableAt(Start.AddDays(1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.CancelAfterStart, result.Error.Code);
    }

    private static Booking BookingStartingAtSeven() => Booking.Create(
        Guid.CreateVersion7(),
        Room.Create(
            Guid.CreateVersion7(),
            "Ada",
            8,
            "Europe/Istanbul",
            BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value).Value,
        "Sprint review",
        "Abdullah",
        TimeSlot.Create(Start, Start.AddHours(1)).Value,
        4,
        Start.AddDays(-1)).Value;
}
