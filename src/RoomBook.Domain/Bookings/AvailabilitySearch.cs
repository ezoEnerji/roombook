using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Domain.Bookings;

/// <summary>
/// Answers "where does a meeting of this length fit in this room?". Pure: it is handed the room, the
/// bookings that touch the window and the current instant, so it needs no clock, no repository and
/// no host to be tested.
/// <para>
/// Each contiguous free stretch contributes its earliest fitting candidate and no more. Returning
/// every quarter hour that fits would be arithmetically complete and practically useless — one free
/// day would produce over a hundred near-identical answers.
/// </para>
/// </summary>
public static class AvailabilitySearch
{
    private static readonly long QuarterHourTicks = TimeSpan.FromMinutes(15).Ticks;

    public static IReadOnlyList<AvailableSlot> Find(
        Room room,
        IReadOnlyList<Booking> bookingsInRoom,
        TimeSlot window,
        TimeSpan duration,
        DateTimeOffset nowUtc,
        int attendeeCount = 1)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(room.TimeZoneId);
        List<AvailableSlot> candidates = [];

        foreach (DateOnly day in LocalDaysCovering(window, zone))
        {
            TimeSlot openingHours = room.BusinessWindowOn(day);
            DateTimeOffset from = Later(openingHours.Start, window.Start);
            DateTimeOffset until = Earlier(openingHours.End, window.End);

            if (until - from < duration)
            {
                continue;
            }

            foreach ((DateTimeOffset stretchStart, DateTimeOffset stretchEnd) in FreeStretches(from, until, bookingsInRoom))
            {
                AvailableSlot? candidate = FirstFittingCandidate(
                    room,
                    stretchStart,
                    stretchEnd,
                    duration,
                    zone,
                    nowUtc,
                    attendeeCount);

                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }
        }

        return candidates;
    }

    private static AvailableSlot? FirstFittingCandidate(
        Room room,
        DateTimeOffset stretchStart,
        DateTimeOffset stretchEnd,
        TimeSpan duration,
        TimeZoneInfo zone,
        DateTimeOffset nowUtc,
        int attendeeCount)
    {
        // The earliest *acceptable* candidate, not merely the earliest one. A stretch that begins at
        // the current instant would otherwise be discarded whole, because BR-8 is strict about "after
        // now" — so the grid is walked forward until a candidate passes or the stretch runs out.
        for (DateTimeOffset start = AlignUpToQuarterHour(stretchStart, zone);
             start + duration <= stretchEnd;
             start = start.AddTicks(QuarterHourTicks))
        {
            Result<TimeSlot> slot = TimeSlot.Create(start, start + duration);
            if (slot.IsFailure)
            {
                continue;
            }

            // Validated by the same function that judges a real booking request, so the search cannot
            // propose something the booking path would refuse. This is what makes "every proposed slot
            // is bookable" a structural property rather than a coincidence.
            Result<Booking> asBooking = Booking.Create(
                Guid.Empty,
                room,
                "availability probe",
                "availability probe",
                slot.Value,
                attendeeCount,
                nowUtc);

            if (asBooking.IsSuccess)
            {
                return new AvailableSlot(room, slot.Value);
            }
        }

        return null;
    }

    /// <summary>
    /// The maximal intervals inside <paramref name="from"/>–<paramref name="until"/> that no booking
    /// touches. Bookings may overlap each other or extend past the edges; the cursor handles both.
    /// </summary>
    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> FreeStretches(
        DateTimeOffset from,
        DateTimeOffset until,
        IReadOnlyList<Booking> bookings)
    {
        IEnumerable<Booking> touching = bookings
            .Where(booking => booking.Slot.Start < until && from < booking.Slot.End)
            .OrderBy(booking => booking.Slot.Start);

        DateTimeOffset cursor = from;

        foreach (Booking booking in touching)
        {
            if (booking.Slot.Start > cursor)
            {
                yield return (cursor, booking.Slot.Start);
            }

            if (booking.Slot.End > cursor)
            {
                cursor = booking.Slot.End;
            }
        }

        if (cursor < until)
        {
            yield return (cursor, until);
        }
    }

    /// <summary>
    /// Rounds up to the next quarter hour on the room's local clock. The instant is converted to
    /// local time and rounded there — generating local times and converting forward instead would
    /// skip or repeat an hour on the days a zone changes offset.
    /// </summary>
    private static DateTimeOffset AlignUpToQuarterHour(DateTimeOffset instant, TimeZoneInfo zone)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(instant, zone);
        long aligned = ((local.Ticks + QuarterHourTicks - 1) / QuarterHourTicks) * QuarterHourTicks;

        return new DateTimeOffset(aligned, local.Offset).ToUniversalTime();
    }

    private static IEnumerable<DateOnly> LocalDaysCovering(TimeSlot window, TimeZoneInfo zone)
    {
        DateOnly first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(window.Start, zone).DateTime);
        DateOnly last = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(window.End, zone).DateTime);

        for (DateOnly day = first; day <= last; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    private static DateTimeOffset Later(DateTimeOffset left, DateTimeOffset right) =>
        left > right ? left : right;

    private static DateTimeOffset Earlier(DateTimeOffset left, DateTimeOffset right) =>
        left < right ? left : right;
}
