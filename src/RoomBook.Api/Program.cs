using System.Text.Json;
using System.Text.Json.Serialization;
using RoomBook.Api.Bookings;
using RoomBook.Api.Infrastructure;
using RoomBook.Api.Problems;
using RoomBook.Api.Rooms;
using RoomBook.Application.Bookings;
using RoomBook.Application.Rooms;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

    // A request carrying a field we do not know is a typo, not a courtesy: reject it rather than
    // silently ignoring it. Proven by the first endpoint with a request body (S-002).
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});

// The clock is a dependency like any other: the application layer receives it and passes the
// instant inward, so no rule ever reads the ambient clock (FD-3).
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IRoomRepository>(_ => new InMemoryRoomRepository(SeedRooms.All));
builder.Services.AddSingleton<IBookingRepository, InMemoryBookingRepository>();
builder.Services.AddScoped<ListRoomsUseCase>();
builder.Services.AddScoped<CreateBookingUseCase>();
builder.Services.AddScoped<GetBookingUseCase>();
builder.Services.AddScoped<SearchAvailabilityUseCase>();

WebApplication app = builder.Build();

app.UseRequestBodyLimit();

app.MapRooms();
app.MapBookings();
app.MapAvailability();

app.Run();

/// <summary>
/// Exposed so the test host can boot the real composition root through
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
