using System.Text.Json;
using System.Text.Json.Serialization;
using RoomBook.Api.Infrastructure;
using RoomBook.Api.Rooms;
using RoomBook.Application.Rooms;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

    // A request carrying a field we do not know is a typo, not a courtesy: reject it rather than
    // silently ignoring it. Proven by the first endpoint with a request body (S-002).
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});

builder.Services.AddSingleton<IRoomRepository>(_ => new InMemoryRoomRepository(SeedRooms.All));
builder.Services.AddScoped<ListRoomsUseCase>();

WebApplication app = builder.Build();

app.MapRooms();

app.Run();

/// <summary>
/// Exposed so the test host can boot the real composition root through
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
