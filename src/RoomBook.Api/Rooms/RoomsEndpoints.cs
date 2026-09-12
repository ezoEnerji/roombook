using RoomBook.Application.Rooms;

namespace RoomBook.Api.Rooms;

/// <summary>
/// The HTTP surface for rooms. Endpoints call use cases and translate — they hold no business rule
/// and never touch a repository (docs/architecture.md, communication rules).
/// </summary>
public static class RoomsEndpoints
{
    public static IEndpointRouteBuilder MapRooms(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/rooms", async (ListRoomsUseCase listRooms, CancellationToken cancellationToken) =>
        {
            IReadOnlyList<Domain.Rooms.Room> rooms = await listRooms.ExecuteAsync(cancellationToken);

            return Results.Ok(rooms.Select(RoomResponse.From).ToList());
        });

        return routes;
    }
}
