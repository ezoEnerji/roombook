using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;

namespace RoomBook.Domain.Tests.Rooms;

/// <summary>
/// BR-1 is judged in the room's own time zone, so the same UTC window can be inside opening hours in
/// one part of the year and outside it in another. Europe/Berlin is used deliberately: Türkiye has had
/// no daylight-saving transition since 2016, so the seeded Istanbul rooms cannot prove this at all.
/// </summary>
public sealed class BusinessHoursEvaluationTests
{
    [Fact]
    public void IsWithinBusinessHours_InWinterWhenBerlinIsOneHourAhead_RejectsTheWindow()
    {
        // 07:30Z is 08:30 in Berlin in January — half an hour before the room opens.
        Assert.False(BerlinRoom().IsWithinBusinessHours(Slot("2026-01-15T07:30:00Z", "2026-01-15T08:30:00Z")));
    }

    [Fact]
    public void IsWithinBusinessHours_InSummerWhenBerlinIsTwoHoursAhead_AcceptsTheSameWindow()
    {
        // The identical UTC window is 09:30–10:30 in Berlin in July: inside opening hours.
        Assert.True(BerlinRoom().IsWithinBusinessHours(Slot("2026-07-15T07:30:00Z", "2026-07-15T08:30:00Z")));
    }

    [Fact]
    public void IsWithinBusinessHours_WhenTheWindowEndsExactlyAtClosingTime_Accepts()
    {
        // 14:00–15:00Z is 17:00–18:00 in Istanbul, and 18:00 is the closing time (BR-1).
        Assert.True(IstanbulRoom().IsWithinBusinessHours(Slot("2026-09-14T14:00:00Z", "2026-09-14T15:00:00Z")));
    }

    [Fact]
    public void IsWithinBusinessHours_WhenTheWindowStartsExactlyAtClosingTime_Rejects()
    {
        Assert.False(IstanbulRoom().IsWithinBusinessHours(Slot("2026-09-14T15:00:00Z", "2026-09-14T15:30:00Z")));
    }

    [Fact]
    public void IsWithinBusinessHours_WhenTheWindowReachesPastClosingTime_Rejects()
    {
        Assert.False(IstanbulRoom().IsWithinBusinessHours(Slot("2026-09-14T14:30:00Z", "2026-09-14T15:30:00Z")));
    }

    [Fact]
    public void IsWithinBusinessHours_WhenTheWindowStartsBeforeOpeningTime_Rejects()
    {
        Assert.False(IstanbulRoom().IsWithinBusinessHours(Slot("2026-09-14T05:30:00Z", "2026-09-14T06:30:00Z")));
    }

    [Fact]
    public void IsWithinBusinessHours_WhenTheWindowCrossesLocalMidnight_Rejects()
    {
        Assert.False(IstanbulRoom().IsWithinBusinessHours(Slot("2026-09-14T20:30:00Z", "2026-09-14T21:30:00Z")));
    }

    private static Room IstanbulRoom() => RoomIn("Europe/Istanbul");

    private static Room BerlinRoom() => RoomIn("Europe/Berlin");

    private static Room RoomIn(string timeZoneId) => Room.Create(
        Guid.CreateVersion7(),
        "Ada",
        8,
        timeZoneId,
        BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value).Value;

    private static TimeSlot Slot(string start, string end) => TimeSlot.Create(
        DateTimeOffset.Parse(start, System.Globalization.CultureInfo.InvariantCulture),
        DateTimeOffset.Parse(end, System.Globalization.CultureInfo.InvariantCulture)).Value;
}
