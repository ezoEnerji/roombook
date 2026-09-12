namespace RoomBook.Application.Bookings;

/// <summary>
/// What a caller asks for, already past the shape checks at the API edge: the window arrives as two
/// instants, and the duration is derived from them rather than sent alongside.
/// </summary>
public sealed record CreateBookingCommand(
    Guid RoomId,
    string Title,
    string Organizer,
    DateTimeOffset Start,
    DateTimeOffset End,
    int AttendeeCount);
