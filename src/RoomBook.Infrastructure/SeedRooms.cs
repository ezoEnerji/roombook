using RoomBook.Domain.Rooms;

namespace RoomBook.Infrastructure;

/// <summary>
/// The rooms of the single office this version serves. Identifiers are fixed literals rather than
/// generated, so a client may store one and still find the room after a restart —
/// <c>Guid.CreateVersion7()</c> is the rule for entities created at runtime, such as bookings.
/// Seed data that breaks a room rule is a programmer error, so construction fails loudly at startup.
/// </summary>
public static class SeedRooms
{
    public static IReadOnlyList<Room> All { get; } =
    [
        Build("0192a1b2-c3d4-7a01-8b01-000000000001", "Ada", 4, new TimeOnly(9, 0), new TimeOnly(18, 0)),
        Build("0192a1b2-c3d4-7a02-8b02-000000000002", "Boğaziçi", 12, new TimeOnly(9, 0), new TimeOnly(18, 0)),
        Build("0192a1b2-c3d4-7a03-8b03-000000000003", "Kapadokya", 24, new TimeOnly(8, 30), new TimeOnly(17, 30)),
    ];

    private const string OfficeTimeZoneId = "Europe/Istanbul";

    private static Room Build(string id, string name, int capacity, TimeOnly opensAt, TimeOnly closesAt)
    {
        BusinessHours hours = BusinessHours.Create(opensAt, closesAt).Value;

        return Room.Create(Guid.Parse(id), name, capacity, OfficeTimeZoneId, hours).Value;
    }
}
