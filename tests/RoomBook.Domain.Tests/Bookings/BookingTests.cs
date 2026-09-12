using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Domain.Tests.Bookings;

/// <summary>
/// BR-4, BR-6, BR-7 and BR-8, each with both of its boundaries, plus the order in which a request
/// that breaks several rules is refused. "Now" arrives as a value, so these tests are deterministic
/// without needing a clock at all.
/// </summary>
public sealed class BookingTests
{
    /// <summary>09:00 in Istanbul, which is UTC+3 all year.</summary>
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 6, 0, 0, TimeSpan.Zero);

    /// <summary>10:00 local, comfortably inside opening hours.</summary>
    private static readonly DateTimeOffset TenLocal = new(2026, 9, 14, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithAValidRequest_ReturnsBooking()
    {
        Result<Booking> result = Create(TenLocal, TenLocal.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal("Sprint review", result.Value.Title);
        Assert.Equal(TenLocal, result.Value.Slot.Start);
    }

    [Fact]
    public void Create_WhenStartIsInThePast_ReturnsStartInPast()
    {
        AssertRefused(ErrorCodes.StartInPast, Create(Now.AddHours(-1), Now.AddMinutes(-30)));
    }

    [Fact]
    public void Create_WhenStartIsExactlyNow_ReturnsStartInPast()
    {
        // BR-8 is strict: "after now", not "not before now".
        AssertRefused(ErrorCodes.StartInPast, Create(Now, Now.AddHours(1)));
    }

    [Fact]
    public void Create_WhenStartIsExactlyNinetyDaysAhead_ReturnsBooking()
    {
        // BR-7 is inclusive: `start <= now + 90 days` is accepted, and this is exactly that instant.
        // It lands on 09:00 local, which is also exactly the opening time — two boundaries at once.
        DateTimeOffset start = Now.AddDays(90);

        Assert.True(Create(start, start.AddHours(1)).IsSuccess);
    }

    [Fact]
    public void Create_WhenStartIsOneMinutePastTheHorizon_ReturnsTooFarInFuture()
    {
        DateTimeOffset start = Now.AddDays(90).AddMinutes(1);

        AssertRefused(ErrorCodes.TooFarInFuture, Create(start, start.AddHours(1)));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(240)]
    public void Create_WhenDurationIsExactlyAtABound_ReturnsBooking(int minutes)
    {
        Assert.True(Create(TenLocal, TenLocal.AddMinutes(minutes)).IsSuccess);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(241)]
    public void Create_WhenDurationIsOneMinuteOutsideABound_ReturnsDurationOutOfRange(int minutes)
    {
        AssertRefused(ErrorCodes.DurationOutOfRange, Create(TenLocal, TenLocal.AddMinutes(minutes)));
    }

    [Fact]
    public void Create_WhenAttendeesEqualTheCapacity_ReturnsBooking()
    {
        Assert.True(Create(TenLocal, TenLocal.AddHours(1), attendees: 8).IsSuccess);
    }

    [Fact]
    public void Create_WhenAttendeesExceedTheCapacity_ReturnsAttendeesExceedCapacity()
    {
        AssertRefused(
            ErrorCodes.AttendeesExceedCapacity,
            Create(TenLocal, TenLocal.AddHours(1), attendees: 9));
    }

    [Fact]
    public void Create_WhenAttendeeCountIsBelowOne_ReturnsRequestInvalid()
    {
        AssertRefused(ErrorCodes.RequestInvalid, Create(TenLocal, TenLocal.AddHours(1), attendees: 0));
    }

    [Fact]
    public void Create_WhenTheWindowEndsExactlyAtClosingTime_ReturnsBooking()
    {
        // 14:00–15:00Z is 17:00–18:00 local, and the room closes at 18:00.
        DateTimeOffset start = new(2026, 9, 14, 14, 0, 0, TimeSpan.Zero);

        Assert.True(Create(start, start.AddHours(1)).IsSuccess);
    }

    [Fact]
    public void Create_WhenTheWindowReachesPastClosingTime_ReturnsOutsideBusinessHours()
    {
        DateTimeOffset start = new(2026, 9, 14, 14, 30, 0, TimeSpan.Zero);

        AssertRefused(ErrorCodes.OutsideBusinessHours, Create(start, start.AddHours(1)));
    }

    [Fact]
    public void Create_WhenTheStartIsInThePastAndTheDurationIsTooLong_ReturnsStartInPast()
    {
        // Precedence: BR-8 is judged before BR-4, so the caller hears about the past first.
        AssertRefused(ErrorCodes.StartInPast, Create(Now.AddHours(-6), Now.AddHours(-1)));
    }

    [Fact]
    public void Create_WhenBeyondTheHorizonAndTooLong_ReturnsTooFarInFuture()
    {
        DateTimeOffset start = Now.AddDays(120);

        AssertRefused(ErrorCodes.TooFarInFuture, Create(start, start.AddHours(6)));
    }

    [Fact]
    public void Create_WhenTooLongAndOutsideBusinessHours_ReturnsDurationOutOfRange()
    {
        // Tomorrow at 03:00Z is 06:00 local, before opening, and six hours is over the limit:
        // BR-4 is judged before BR-1, so duration wins. Tomorrow, not today — today at 03:00Z is
        // already in the past, and BR-8 would win instead.
        DateTimeOffset start = new(2026, 9, 15, 3, 0, 0, TimeSpan.Zero);

        AssertRefused(ErrorCodes.DurationOutOfRange, Create(start, start.AddHours(6)));
    }

    [Fact]
    public void Create_WhenOutsideBusinessHoursAndOverCapacity_ReturnsOutsideBusinessHours()
    {
        DateTimeOffset start = new(2026, 9, 15, 3, 0, 0, TimeSpan.Zero);

        AssertRefused(
            ErrorCodes.OutsideBusinessHours,
            Create(start, start.AddHours(1), attendees: 99));
    }

    private static void AssertRefused(string expectedCode, Result<Booking> result)
    {
        Assert.True(result.IsFailure, "Expected the booking to be refused.");
        Assert.Equal(expectedCode, result.Error.Code);
    }

    private static Result<Booking> Create(DateTimeOffset start, DateTimeOffset end, int attendees = 4) =>
        Booking.Create(
            Guid.CreateVersion7(),
            IstanbulRoom(),
            "Sprint review",
            "Abdullah",
            TimeSlot.Create(start, end).Value,
            attendees,
            Now);

    private static Room IstanbulRoom() => Room.Create(
        Guid.CreateVersion7(),
        "Ada",
        8,
        "Europe/Istanbul",
        BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value).Value;
}
