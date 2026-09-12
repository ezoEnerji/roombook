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
