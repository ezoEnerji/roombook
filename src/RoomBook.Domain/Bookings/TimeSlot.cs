using RoomBook.Domain.Shared;

namespace RoomBook.Domain.Bookings;

/// <summary>
/// A half-open interval in UTC: <c>[Start, End)</c>. The half-open form is the whole reason
/// back-to-back bookings are legal (BR-3) — two slots that merely touch do not intersect.
/// </summary>
public sealed record TimeSlot
{
    private TimeSlot(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Rejects a window that does not move forwards. That is a malformed request rather than a
    /// duration question, so it carries the request code and BR-4 never sees it.
    /// </summary>
    public static Result<TimeSlot> Create(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            return Result<TimeSlot>.Failure(
                ErrorCodes.RequestInvalid,
                "A booking must end after it starts.");
        }

        return Result<TimeSlot>.Success(new TimeSlot(start.ToUniversalTime(), end.ToUniversalTime()));
    }

    /// <summary>
    /// True when the two windows intersect. Touching endpoints do not intersect: 10:00–11:00 and
    /// 11:00–12:00 are both welcome in the same room (BR-3).
    /// </summary>
    public bool Overlaps(TimeSlot other) => Start < other.End && other.Start < End;
}
