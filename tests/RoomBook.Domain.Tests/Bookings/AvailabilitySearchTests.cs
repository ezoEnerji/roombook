using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;

namespace RoomBook.Domain.Tests.Bookings;

/// <summary>
/// The search algorithm. Istanbul is UTC+3 all year, so local 09:00–18:00 is 06:00Z–15:00Z and the
/// arithmetic in these tests stays readable; the daylight-saving case uses Berlin on purpose.
/// </summary>
public sealed class AvailabilitySearchTests
{
    /// <summary>Monday 09:00 local.</summary>
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 6, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    [Fact]
    public void Find_WhenTheDayIsEmpty_ProposesTheOpeningSlot()
    {
        IReadOnlyList<AvailableSlot> found = Find(Tuesday(), OneHour);

        AvailableSlot only = Assert.Single(found);
        Assert.Equal(Utc("2026-09-15T06:00:00Z"), only.Slot.Start);
        Assert.Equal(Utc("2026-09-15T07:00:00Z"), only.Slot.End);
    }

    [Fact]
    public void Find_WhenABookingSitsMidMorning_ProposesOneCandidateEitherSide()
    {
        // The booking runs 10:00–11:00 local, splitting the day into two free stretches.
        IReadOnlyList<AvailableSlot> found = Find(
            Tuesday(),
            OneHour,
            BookingAt("2026-09-15T07:00:00Z", "2026-09-15T08:00:00Z"));

        Assert.Equal(2, found.Count);
        Assert.Equal(Utc("2026-09-15T06:00:00Z"), found[0].Slot.Start);

        // BR-3: the second candidate begins exactly when the booking ends.
        Assert.Equal(Utc("2026-09-15T08:00:00Z"), found[1].Slot.Start);
    }

    [Fact]
    public void Find_WhenABookingIsNotOnTheGrid_KeepsCandidatesOnTheQuarterHour()
    {
        // A booking from 10:07 to 11:07 local does not move the grid: the next candidate is 11:15.
        IReadOnlyList<AvailableSlot> found = Find(
            Tuesday(),
            OneHour,
            BookingAt("2026-09-15T07:07:00Z", "2026-09-15T08:07:00Z"));

        Assert.Equal(2, found.Count);
        Assert.Equal(Utc("2026-09-15T06:00:00Z"), found[0].Slot.Start);
        Assert.Equal(Utc("2026-09-15T08:15:00Z"), found[1].Slot.Start);
    }

    [Fact]
    public void Find_WhenAStretchIsTooShortForTheMeeting_ProposesNothingForIt()
    {
        // Free until 09:30 local, then booked to closing: half an hour cannot hold an hour. The
        // bookings come in four-hour pieces because BR-4 caps a single booking at four hours.
        IReadOnlyList<AvailableSlot> found = Find(
            Tuesday(),
            OneHour,
            BookingAt("2026-09-15T06:30:00Z", "2026-09-15T10:30:00Z"),
            BookingAt("2026-09-15T10:30:00Z", "2026-09-15T14:30:00Z"),
            BookingAt("2026-09-15T14:30:00Z", "2026-09-15T15:00:00Z"));

        Assert.Empty(found);
    }

    [Fact]
    public void Find_WhenTheDayIsFullyBooked_ProposesNothing()
    {
        IReadOnlyList<AvailableSlot> found = Find(
            Tuesday(),
            OneHour,
            BookingAt("2026-09-15T06:00:00Z", "2026-09-15T10:00:00Z"),
            BookingAt("2026-09-15T10:00:00Z", "2026-09-15T14:00:00Z"),
            BookingAt("2026-09-15T14:00:00Z", "2026-09-15T15:00:00Z"));

        Assert.Empty(found);
    }

    [Fact]
    public void Find_WhenTheWindowStartsBeforeNow_ProposesNothingEarlierThanNow()
    {
        // Today's window opened at 09:00 local, which is exactly "now", and BR-8 is strict about
        // starting after now — so the first acceptable candidate is the next quarter hour.
        IReadOnlyList<AvailableSlot> found = Find(
            SlotOf("2026-09-14T00:00:00Z", "2026-09-14T23:00:00Z"),
            OneHour);

        AvailableSlot only = Assert.Single(found);
        Assert.Equal(Utc("2026-09-14T06:15:00Z"), only.Slot.Start);
        Assert.True(only.Slot.Start > Now);
    }

    [Fact]
    public void Find_WhenTheWindowIsEntirelyInThePast_ProposesNothing()
    {
        IReadOnlyList<AvailableSlot> found = Find(
            SlotOf("2026-09-10T00:00:00Z", "2026-09-11T00:00:00Z"),
            OneHour);

        Assert.Empty(found);
    }

    [Fact]
    public void Find_WhenTheWindowReachesPastTheHorizon_ProposesNothingBeyondIt()
    {
        // The horizon is 90 days from now; this window sits entirely past it.
        IReadOnlyList<AvailableSlot> found = Find(
            SlotOf("2027-01-11T00:00:00Z", "2027-01-12T00:00:00Z"),
            OneHour);

        Assert.Empty(found);
    }

    [Fact]
    public void Find_WhenTheMeetingFillsTheWholeDay_ProposesTheOnlyWindowThatFits()
    {
        // Istanbul rooms are open nine hours; a four-hour meeting fits from the opening minute.
        IReadOnlyList<AvailableSlot> found = Find(Tuesday(), TimeSpan.FromHours(4));

        AvailableSlot only = Assert.Single(found);
        Assert.Equal(Utc("2026-09-15T06:00:00Z"), only.Slot.Start);
        Assert.Equal(TimeSpan.FromHours(4), only.Slot.Duration);
    }

    [Fact]
    public void Find_AcrossSeveralDays_ProposesOneCandidatePerDay()
    {
        IReadOnlyList<AvailableSlot> found = Find(
            SlotOf("2026-09-15T00:00:00Z", "2026-09-17T23:00:00Z"),
            OneHour);

        Assert.Equal(3, found.Count);
        Assert.Equal(
            [Utc("2026-09-15T06:00:00Z"), Utc("2026-09-16T06:00:00Z"), Utc("2026-09-17T06:00:00Z")],
            found.Select(candidate => candidate.Slot.Start));
    }

    [Fact]
    public void Find_WhenTheRoomIsTooSmallForTheGroup_ProposesNothing()
    {
        // Capacity is enforced by the same rules that judge a booking, so an oversized group gets no
        // proposals rather than proposals it could not book.
        IReadOnlyList<AvailableSlot> found = AvailabilitySearch.Find(
            IstanbulRoom(),
            [],
            Tuesday(),
            OneHour,
            Now,
            attendeeCount: 99);

        Assert.Empty(found);
    }

    [Fact]
    public void Find_ForARoomOnAHalfHourOffset_AlignsToItsLocalQuarterHour()
    {
        // Asia/Kolkata is UTC+05:30, so its local quarter hours land on :00 and :30 past the UTC
        // hour. Alignment that quietly used UTC would still look plausible — and be half an hour out.
        Room kolkata = RoomIn("Asia/Kolkata");

        IReadOnlyList<AvailableSlot> found = AvailabilitySearch.Find(
            kolkata,
            [],
            Tuesday(),
            OneHour,
            Now);

        AvailableSlot only = Assert.Single(found);
        Assert.Equal(Utc("2026-09-15T03:30:00Z"), only.Slot.Start);
    }

    [Fact]
    public void Find_ForARoomOnAHalfHourOffset_SkipsAnOffGridBookingToTheNextLocalQuarter()
    {
        Room kolkata = RoomIn("Asia/Kolkata");
        Booking booked = Booking.Create(
            Guid.CreateVersion7(),
            kolkata,
            "Existing",
            "Abdullah",
            SlotOf("2026-09-15T05:07:00Z", "2026-09-15T06:07:00Z"),
            4,
            Now.AddDays(-1)).Value;

        IReadOnlyList<AvailableSlot> found = AvailabilitySearch.Find(
            kolkata,
            [booked],
            Tuesday(),
            OneHour,
            Now);

        Assert.Equal(2, found.Count);

        // The room opens at 09:00 local, which is 03:30Z on a half-hour offset.
        Assert.Equal(Utc("2026-09-15T03:30:00Z"), found[0].Slot.Start);

        // 06:07Z is 11:37 in Kolkata; the next local quarter hour is 11:45, which is 06:15Z.
        Assert.Equal(Utc("2026-09-15T06:15:00Z"), found[1].Slot.Start);
    }

    [Fact]
    public void Find_OnADaylightSavingDay_StaysOnTheRoomsLocalQuarterHour()
    {
        Room berlin = Room.Create(
            Guid.CreateVersion7(),
            "Brandenburg",
            8,
            "Europe/Berlin",
            BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value).Value;

        // 2026-03-29 is the day Berlin moves to summer time, hours before the room opens.
        IReadOnlyList<AvailableSlot> found = AvailabilitySearch.Find(
            berlin,
            [],
            SlotOf("2026-03-29T00:00:00Z", "2026-03-29T23:00:00Z"),
            OneHour,
            new DateTimeOffset(2026, 3, 20, 6, 0, 0, TimeSpan.Zero));

        AvailableSlot only = Assert.Single(found);
        Assert.Equal(Utc("2026-03-29T07:00:00Z"), only.Slot.Start);
    }

    private static IReadOnlyList<AvailableSlot> Find(TimeSlot window, TimeSpan duration, params Booking[] bookings) =>
        AvailabilitySearch.Find(IstanbulRoom(), bookings, window, duration, Now);

    private static TimeSlot Tuesday() => SlotOf("2026-09-15T00:00:00Z", "2026-09-15T23:00:00Z");

    private static TimeSlot SlotOf(string start, string end) => TimeSlot.Create(Utc(start), Utc(end)).Value;

    private static DateTimeOffset Utc(string instant) =>
        DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture);

    private static Booking BookingAt(string start, string end) => Booking.Create(
        Guid.CreateVersion7(),
        IstanbulRoom(),
        "Existing",
        "Abdullah",
        SlotOf(start, end),
        4,
        Now.AddDays(-1)).Value;

    private static Room IstanbulRoom() => RoomIn("Europe/Istanbul");

    private static Room RoomIn(string timeZoneId) => Room.Create(
        Guid.Parse("0192a1b2-c3d4-7a01-8b01-000000000001"),
        "Ada",
        8,
        timeZoneId,
        BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value).Value;
}
