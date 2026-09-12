using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Domain.Bookings;

/// <summary>
/// A reservation of one room for one <see cref="TimeSlot"/>. Every rule that can be judged from the
/// request and the room is evaluated here, in the order documented in the spec, so a caller always
/// learns about the same violation first and the tests are stable. The one rule missing is BR-2: an
/// overlap can only be judged against the other bookings, so it is enforced where they are stored.
/// </summary>
public sealed record Booking
{
    /// <summary>BR-4, lower bound.</summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(15);

    /// <summary>BR-4, upper bound.</summary>
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(4);

    /// <summary>BR-7: how far ahead a booking may start.</summary>
    public static readonly TimeSpan BookingHorizon = TimeSpan.FromDays(90);

    private Booking(Guid id, Guid roomId, string title, string organizer, TimeSlot slot, int attendeeCount)
    {
        Id = id;
        RoomId = roomId;
        Title = title;
        Organizer = organizer;
        Slot = slot;
        AttendeeCount = attendeeCount;
    }

    public Guid Id { get; }

    public Guid RoomId { get; }

    public string Title { get; }

    public string Organizer { get; }

    public TimeSlot Slot { get; }

    public int AttendeeCount { get; }

    public static Result<Booking> Create(
        Guid id,
        Room room,
        string title,
        string organizer,
        TimeSlot slot,
        int attendeeCount,
        DateTimeOffset nowUtc)
    {
        if (attendeeCount < 1)
        {
            return Failure(ErrorCodes.RequestInvalid, "A booking needs at least one attendee.");
        }

        if (slot.Start <= nowUtc)
        {
            return Failure(ErrorCodes.StartInPast, "A booking must start after now.");
        }

        if (slot.Start > nowUtc + BookingHorizon)
        {
            return Failure(
                ErrorCodes.TooFarInFuture,
                $"A booking may start at most {BookingHorizon.Days} days from now.");
        }

        if (slot.Duration < MinimumDuration || slot.Duration > MaximumDuration)
        {
            return Failure(
                ErrorCodes.DurationOutOfRange,
                $"A booking lasts between {MinimumDuration.TotalMinutes:0} minutes and {MaximumDuration.TotalHours:0} hours.");
        }

        if (!room.IsWithinBusinessHours(slot))
        {
            return Failure(
                ErrorCodes.OutsideBusinessHours,
                $"Room '{room.Name}' is open from {room.Hours.Open:HH\\:mm} to {room.Hours.Close:HH\\:mm} local time.");
        }

        if (attendeeCount > room.Capacity)
        {
            return Failure(
                ErrorCodes.AttendeesExceedCapacity,
                $"Room '{room.Name}' holds {room.Capacity} people.");
        }

        return Result<Booking>.Success(new Booking(id, room.Id, title, organizer, slot, attendeeCount));
    }

    private static Result<Booking> Failure(string code, string message) =>
        Result<Booking>.Failure(code, message);
}
