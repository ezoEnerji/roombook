using RoomBook.Domain.Bookings;

namespace RoomBook.Api.Bookings;

/// <summary>
/// The wire shape of a booking — the API's own record, never the domain type (FD-5). Times go out as
/// UTC instants (BR-5); the room is referenced by identifier rather than embedded.
/// </summary>
public sealed record BookingResponse(
    Guid Id,
    Guid RoomId,
    string Title,
    string Organizer,
    DateTimeOffset Start,
    DateTimeOffset End,
    int AttendeeCount)
{
    public static BookingResponse From(Booking booking) => new(
        booking.Id,
        booking.RoomId,
        booking.Title,
        booking.Organizer,
        booking.Slot.Start,
        booking.Slot.End,
        booking.AttendeeCount);
}
