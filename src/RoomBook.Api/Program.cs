using System.Text.Json;
using System.Text.Json.Serialization;
using RoomBook.Api.Bookings;
using RoomBook.Api.Problems;
using RoomBook.Api.Rooms;
using RoomBook.Application.Bookings;
using RoomBook.Application.Rooms;
using RoomBook.Infrastructure;

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

// Built here rather than resolved from the container later: the store owns the connection that keeps
// an in-memory database alive, and asking the container for it after Build() would be the service
// locator FD-4 forbids. The schema and the seeded rooms are ensured once, so a fresh checkout needs
// no setup command and a restart neither loses a booking nor duplicates a room.
RoomBookStore store = new(builder.Configuration.GetConnectionString("RoomBook") ?? "Data Source=roombook.db");
store.EnsureCreated();

builder.Services.AddSingleton(store);
builder.Services.AddSingleton<IRoomRepository, SqliteRoomRepository>();
builder.Services.AddSingleton<IBookingRepository, SqliteBookingRepository>();
builder.Services.AddScoped<ListRoomsUseCase>();
builder.Services.AddScoped<CreateBookingUseCase>();
builder.Services.AddScoped<GetBookingUseCase>();
builder.Services.AddScoped<SearchAvailabilityUseCase>();
builder.Services.AddScoped<CancelBookingUseCase>();
builder.Services.AddScoped<ListBookingsUseCase>();

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
