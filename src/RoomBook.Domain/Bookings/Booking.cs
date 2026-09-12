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

    /// <summary>
    /// Rebuilds a booking that was accepted earlier and stored. The rules are deliberately **not**
    /// re-run: they were judged when the booking was created, against the instant that applied then.
    /// Judging them again here would reject every booking the moment it starts — the opposite of
    /// remembering it — and would make reading the past depend on the present.
    /// <para>
    /// This exists because a store has to be able to hand a booking back. Persistence ignorance means
    /// the domain knows nothing about the store; it does not mean the store can rebuild a value
    /// without being given a way in.
    /// </para>
    /// </summary>
    public static Booking Rehydrate(
        Guid id,
        Guid roomId,
        string title,
        string organizer,
        TimeSlot slot,
        int attendeeCount) =>
        new(id, roomId, title, organizer, slot, attendeeCount);

    /// <summary>
    /// BR-9: a booking may be cancelled only before it starts. A meeting already in progress is not
    /// something the system pretends never happened, so the refusal is a rule violation rather than
    /// a missing record — which is what a caller cancelling twice gets instead.
    /// </summary>
    public Result<Booking> EnsureCancellableAt(DateTimeOffset nowUtc) => Slot.Start <= nowUtc
        ? Result<Booking>.Failure(
            ErrorCodes.CancelAfterStart,
            "A booking can only be cancelled before it starts.")
        : Result<Booking>.Success(this);

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
