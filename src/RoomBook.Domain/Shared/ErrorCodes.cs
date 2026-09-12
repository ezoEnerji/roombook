namespace RoomBook.Domain.Shared;

/// <summary>
/// The error codes a caller can see. Each one maps to exactly one HTTP status in
/// `docs/conventions.md`, and every code here is part of the public contract — renaming one breaks
/// clients. Codes that can only be produced by bad seed data (see <c>Room</c>) stay with their type,
/// because they never reach the wire.
/// </summary>
public static class ErrorCodes
{
    public const string RequestInvalid = "request.invalid";
    public const string RoomNotFound = "room.not_found";
    public const string BookingNotFound = "booking.not_found";
    public const string BookingOverlap = "booking.overlap";
    public const string OutsideBusinessHours = "booking.outside_business_hours";
    public const string DurationOutOfRange = "booking.duration_out_of_range";
    public const string AttendeesExceedCapacity = "booking.attendees_exceed_capacity";
    public const string TooFarInFuture = "booking.too_far_in_future";
    public const string StartInPast = "booking.start_in_past";
}
