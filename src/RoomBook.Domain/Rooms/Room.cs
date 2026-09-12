using RoomBook.Domain.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Domain.Rooms;

/// <summary>
/// A bookable meeting room. A room that could never be booked successfully cannot exist: it holds
/// at least one person, its IANA time zone resolves on this machine, and it opens before it closes.
/// The time zone is kept as an identifier so the room stays a plain value; <see cref="TimeZoneInfo"/>
/// is resolved where a calculation needs it.
/// </summary>
public sealed record Room
{
    public const string CapacityBelowMinimumCode = "room.capacity_below_minimum";
    public const string TimeZoneUnknownCode = "room.time_zone_unknown";

    private Room(Guid id, string name, int capacity, string timeZoneId, BusinessHours hours)
    {
        Id = id;
        Name = name;
        Capacity = capacity;
        TimeZoneId = timeZoneId;
        Hours = hours;
    }

    public Guid Id { get; }

    public string Name { get; }

    public int Capacity { get; }

    public string TimeZoneId { get; }

    public BusinessHours Hours { get; }

    /// <summary>
    /// BR-1: the window lies inside this room's opening hours, judged in this room's own time zone.
    /// The UTC instants are converted here, which is why a daylight-saving change moves the UTC range
    /// a room accepts without moving its opening time.
    /// </summary>
    public bool IsWithinBusinessHours(TimeSlot slot)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        DateTime startLocal = TimeZoneInfo.ConvertTime(slot.Start, zone).DateTime;
        DateTime endLocal = TimeZoneInfo.ConvertTime(slot.End, zone).DateTime;

        if (startLocal.Date != endLocal.Date)
        {
            // Opening hours describe one local day, so a window that crosses local midnight cannot
            // fit inside them however short it is.
            return false;
        }

        TimeOnly start = TimeOnly.FromDateTime(startLocal);
        TimeOnly end = TimeOnly.FromDateTime(endLocal);

        return start >= Hours.Open && start < Hours.Close && end <= Hours.Close;
    }

    /// <summary>
    /// The UTC window this room's opening hours occupy on one of its local days. This is the only
    /// place a whole day is converted, so daylight-saving arithmetic lives in one method instead of
    /// being repeated by every caller that needs to know when a room is open.
    /// </summary>
    public TimeSlot BusinessWindowOn(DateOnly localDate)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

        return TimeSlot.Create(ToUtc(localDate, Hours.Open, zone), ToUtc(localDate, Hours.Close, zone)).Value;
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        DateTime local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            // The local clock skipped this time when the offset changed, so the room opens at the
            // first moment that does exist. Only reachable if a zone shifts during business hours.
            local = local.Add(zone.GetAdjustmentRules()
                .First(rule => rule.DateStart <= local && local <= rule.DateEnd)
                .DaylightDelta);
        }

        // An ambiguous local time (the hour that happens twice) resolves to the standard offset,
        // which is what TimeZoneInfo returns and is deterministic either way.
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public static Result<Room> Create(
        Guid id,
        string name,
        int capacity,
        string timeZoneId,
        BusinessHours hours)
    {
        if (capacity < 1)
        {
            return Result<Room>.Failure(
                CapacityBelowMinimumCode,
                "A room must hold at least one person.");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
        {
            return Result<Room>.Failure(
                TimeZoneUnknownCode,
                $"Time zone '{timeZoneId}' cannot be resolved on this system.");
        }

        return Result<Room>.Success(new Room(id, name, capacity, timeZoneId, hours));
    }
}
