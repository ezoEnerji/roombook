namespace RoomBook.Application.Bookings;

/// <summary>
/// What the caller wants to know: how long the meeting is, when it could happen, and optionally
/// which room and how many people. Both ends of the window are required — a default would answer a
/// question nobody asked.
/// </summary>
public sealed record SearchAvailabilityQuery(
    int DurationMinutes,
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? RoomId,
    int? AttendeeCount);
