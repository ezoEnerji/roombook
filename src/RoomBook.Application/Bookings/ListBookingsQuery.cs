namespace RoomBook.Application.Bookings;

/// <summary>
/// Which bookings the caller wants to see: a window, and optionally one room. The window is required
/// for the same reason it is required when searching — an unbounded listing answers a question
/// nobody asked and grows without limit.
/// </summary>
public sealed record ListBookingsQuery(DateTimeOffset From, DateTimeOffset To, Guid? RoomId);
