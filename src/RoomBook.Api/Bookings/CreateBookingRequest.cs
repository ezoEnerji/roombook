using RoomBook.Application.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Api.Bookings;

/// <summary>
/// What arrives on the wire. Every member is nullable so a missing field is detectable rather than
/// silently defaulted — an absent attendee count that became 1 would quietly disable BR-6.
/// </summary>
public sealed record CreateBookingRequest(
    Guid? RoomId,
    string? Title,
    string? Organizer,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    int? AttendeeCount)
{
    private const int TitleLimit = 200;
    private const int OrganizerLimit = 100;

    /// <summary>
    /// Shape only: presence, length, range, and UTC on the wire. Business rules are judged once,
    /// inside the domain, so nothing here duplicates a decision the domain also makes.
    /// </summary>
    public Result<CreateBookingCommand> ToCommand()
    {
        if (RoomId is null || Start is null || End is null || AttendeeCount is null)
        {
            return Invalid("roomId, start, end and attendeeCount are all required.");
        }

        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Organizer))
        {
            return Invalid("title and organizer are required.");
        }

        if (Title.Length > TitleLimit)
        {
            return Invalid($"title is limited to {TitleLimit} characters.");
        }

        if (Organizer.Length > OrganizerLimit)
        {
            return Invalid($"organizer is limited to {OrganizerLimit} characters.");
        }

        if (Start.Value.Offset != TimeSpan.Zero || End.Value.Offset != TimeSpan.Zero)
        {
            return Invalid("start and end must be UTC instants ending in 'Z'.");
        }

        if (AttendeeCount.Value < 1)
        {
            return Invalid("attendeeCount must be at least 1.");
        }

        return Result<CreateBookingCommand>.Success(new CreateBookingCommand(
            RoomId.Value,
            Title,
            Organizer,
            Start.Value,
            End.Value,
            AttendeeCount.Value));
    }

    private static Result<CreateBookingCommand> Invalid(string message) =>
        Result<CreateBookingCommand>.Failure(ErrorCodes.RequestInvalid, message);
}
