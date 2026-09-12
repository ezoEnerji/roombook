using RoomBook.Domain.Shared;

namespace RoomBook.Domain.Rooms;

/// <summary>
/// The half-open local-time window <c>[Open, Close)</c> in which a room may be booked. These are
/// recurring wall-clock facts, not instants: they are never converted to UTC, because doing so
/// would silently shift a room's opening time on daylight-saving days.
/// </summary>
public sealed record BusinessHours
{
    public const string InvalidWindowCode = "room.hours_invalid";

    private BusinessHours(TimeOnly open, TimeOnly close)
    {
        Open = open;
        Close = close;
    }

    public TimeOnly Open { get; }

    public TimeOnly Close { get; }

    public static Result<BusinessHours> Create(TimeOnly open, TimeOnly close)
    {
        if (open >= close)
        {
            return Result<BusinessHours>.Failure(
                InvalidWindowCode,
                "A room must open strictly before it closes.");
        }

        return Result<BusinessHours>.Success(new BusinessHours(open, close));
    }
}
