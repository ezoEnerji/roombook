using RoomBook.Domain.Rooms;

namespace RoomBook.Domain.Bookings;

/// <summary>
/// A window the search proposes: the room it belongs to and the time it occupies. Computed on every
/// request and never stored — and never held, so two callers may be shown the same slot and BR-2
/// decides who gets it.
/// </summary>
public sealed record AvailableSlot(Room Room, TimeSlot Slot);
