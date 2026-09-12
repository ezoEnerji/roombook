using RoomBook.Domain.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Application.Bookings;

/// <summary>
/// The window rule every time-ranged query shares: it must move forwards, and it may cover at most
/// 31 days (`docs/security.md`). One function rather than a convention, so the availability search
/// and a booking listing cannot come to disagree about what a valid window is.
/// </summary>
public static class QueryWindow
{
    public static readonly TimeSpan Max = TimeSpan.FromDays(31);

    public static Result<TimeSlot> Create(DateTimeOffset from, DateTimeOffset to)
    {
        Result<TimeSlot> window = TimeSlot.Create(from, to);

        if (window.IsFailure)
        {
            return window;
        }

        return window.Value.Duration > Max
            ? Result<TimeSlot>.Failure(ErrorCodes.RequestInvalid, $"A query covers at most {Max.Days} days.")
            : window;
    }
}
