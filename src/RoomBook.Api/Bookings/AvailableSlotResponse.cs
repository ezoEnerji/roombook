using RoomBook.Domain.Bookings;

namespace RoomBook.Api.Bookings;

/// <summary>
/// A proposed window on the wire. The API's own record (FD-5), carrying the room's name as well as
/// its identifier so a caller can show the answer without a second request.
/// </summary>
public sealed record AvailableSlotResponse(
    Guid RoomId,
    string RoomName,
    DateTimeOffset Start,
    DateTimeOffset End)
{
    public static AvailableSlotResponse From(AvailableSlot slot) => new(
        slot.Room.Id,
        slot.Room.Name,
        slot.Slot.Start,
        slot.Slot.End);
}
