using System.Globalization;
using RoomBook.Domain.Rooms;

namespace RoomBook.Api.Rooms;

/// <summary>
/// The wire shape of a room. The API owns its own DTOs — domain types are never serialised into an
/// HTTP body (FD-5). Opening and closing times are local wall-clock values paired with
/// <see cref="TimeZone"/>, never UTC instants.
/// </summary>
public sealed record RoomResponse(
    Guid Id,
    string Name,
    int Capacity,
    string TimeZone,
    string OpensAt,
    string ClosesAt)
{
    public static RoomResponse From(Room room) => new(
        room.Id,
        room.Name,
        room.Capacity,
        room.TimeZoneId,
        Format(room.Hours.Open),
        Format(room.Hours.Close));

    private static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
