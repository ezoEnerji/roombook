using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;

namespace RoomBook.Domain.Tests.Rooms;

/// <summary>
/// The daily opening window in UTC. This is where daylight saving either works or quietly ruins the
/// availability search, so both sides of a European transition are pinned here.
/// </summary>
public sealed class BusinessWindowTests
{
    [Fact]
    public void BusinessWindowOn_ForAnIstanbulRoom_IsThreeHoursBehindLocalTime()
    {
        TimeSlot window = RoomIn("Europe/Istanbul").BusinessWindowOn(new DateOnly(2026, 9, 15));

        Assert.Equal(new DateTimeOffset(2026, 9, 15, 6, 0, 0, TimeSpan.Zero), window.Start);
        Assert.Equal(new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero), window.End);
    }

    [Fact]
    public void BusinessWindowOn_ForABerlinRoomInWinter_IsOneHourBehindLocalTime()
    {
        TimeSlot window = RoomIn("Europe/Berlin").BusinessWindowOn(new DateOnly(2026, 1, 15));

        Assert.Equal(new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero), window.Start);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 17, 0, 0, TimeSpan.Zero), window.End);
    }

    [Fact]
    public void BusinessWindowOn_ForABerlinRoomInSummer_IsTwoHoursBehindLocalTime()
    {
        // Same room, same opening hours, a different UTC window: the hours did not move, the offset did.
        TimeSlot window = RoomIn("Europe/Berlin").BusinessWindowOn(new DateOnly(2026, 7, 15));

        Assert.Equal(new DateTimeOffset(2026, 7, 15, 7, 0, 0, TimeSpan.Zero), window.Start);
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 16, 0, 0, TimeSpan.Zero), window.End);
    }

    [Fact]
    public void BusinessWindowOn_OnTheDayBerlinSwitchesToSummerTime_UsesTheNewOffset()
    {
        // The clocks go forward at 02:00 local, hours before the room opens.
        TimeSlot window = RoomIn("Europe/Berlin").BusinessWindowOn(new DateOnly(2026, 3, 29));

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 7, 0, 0, TimeSpan.Zero), window.Start);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 16, 0, 0, TimeSpan.Zero), window.End);
    }

    [Fact]
    public void BusinessWindowOn_OnTheDayBerlinSwitchesBack_UsesTheStandardOffset()
    {
        TimeSlot window = RoomIn("Europe/Berlin").BusinessWindowOn(new DateOnly(2026, 10, 25));

        Assert.Equal(new DateTimeOffset(2026, 10, 25, 8, 0, 0, TimeSpan.Zero), window.Start);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 17, 0, 0, TimeSpan.Zero), window.End);
    }

    private static Room RoomIn(string timeZoneId) => Room.Create(
        Guid.CreateVersion7(),
        "Ada",
        8,
        timeZoneId,
        BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value).Value;
}
