using RoomBook.Domain.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Domain.Tests.Bookings;

public sealed class TimeSlotTests
{
    private static readonly DateTimeOffset Ten = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Eleven = new(2026, 9, 14, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Twelve = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WhenEndIsAfterStart_ReturnsSlot()
    {
        Result<TimeSlot> result = TimeSlot.Create(Ten, Eleven);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromHours(1), result.Value.Duration);
    }

    [Fact]
    public void Create_WhenEndEqualsStart_ReturnsRequestInvalid()
    {
        Result<TimeSlot> result = TimeSlot.Create(Ten, Ten);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.RequestInvalid, result.Error.Code);
    }

    [Fact]
    public void Create_WhenEndIsBeforeStart_ReturnsRequestInvalid()
    {
        Result<TimeSlot> result = TimeSlot.Create(Eleven, Ten);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.RequestInvalid, result.Error.Code);
    }

    [Fact]
    public void Create_WhenGivenAnOffset_NormalisesToUtc()
    {
        DateTimeOffset localNoon = new(2026, 9, 14, 13, 0, 0, TimeSpan.FromHours(3));

        Result<TimeSlot> result = TimeSlot.Create(localNoon, localNoon.AddHours(1));

        Assert.Equal(TimeSpan.Zero, result.Value.Start.Offset);
        Assert.Equal(Ten, result.Value.Start);
    }

    [Fact]
    public void Overlaps_WhenOneStartsExactlyWhenTheOtherEnds_IsFalse()
    {
        // BR-3. This is the single case the whole half-open design exists for.
        TimeSlot earlier = Slot(Ten, Eleven);
        TimeSlot later = Slot(Eleven, Twelve);

        Assert.False(earlier.Overlaps(later));
        Assert.False(later.Overlaps(earlier));
    }

    [Fact]
    public void Overlaps_WhenTheyShareAMinute_IsTrue()
    {
        TimeSlot earlier = Slot(Ten, Eleven);
        TimeSlot later = Slot(Eleven.AddMinutes(-1), Twelve);

        Assert.True(earlier.Overlaps(later));
        Assert.True(later.Overlaps(earlier));
    }

    [Fact]
    public void Overlaps_WhenOneContainsTheOther_IsTrue()
    {
        TimeSlot outer = Slot(Ten, Twelve);
        TimeSlot inner = Slot(Ten.AddMinutes(15), Ten.AddMinutes(45));

        Assert.True(outer.Overlaps(inner));
        Assert.True(inner.Overlaps(outer));
    }

    [Fact]
    public void Overlaps_WhenIdentical_IsTrue()
    {
        Assert.True(Slot(Ten, Eleven).Overlaps(Slot(Ten, Eleven)));
    }

    [Fact]
    public void Overlaps_WhenSeparatedByAnHour_IsFalse()
    {
        Assert.False(Slot(Ten, Eleven).Overlaps(Slot(Twelve, Twelve.AddHours(1))));
    }

    private static TimeSlot Slot(DateTimeOffset start, DateTimeOffset end) => TimeSlot.Create(start, end).Value;
}
