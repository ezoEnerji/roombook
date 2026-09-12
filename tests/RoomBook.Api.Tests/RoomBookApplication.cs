using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomBook.Infrastructure;

namespace RoomBook.Api.Tests;

/// <summary>
/// Boots the real composition root against a store of its own. Every host gets a uniquely named
/// in-memory SQLite database, so two tests can never see each other's bookings; a test that needs to
/// prove persistence passes a file instead, because nothing in memory survives a restart.
/// </summary>
internal sealed class RoomBookApplication : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly TimeProvider? _clock;

    public RoomBookApplication(TimeProvider? clock = null, string? connectionString = null)
    {
        _clock = clock;
        _connectionString = connectionString
            ?? $"Data Source=roombook-{Guid.CreateVersion7():N};Mode=Memory;Cache=Shared";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TimeProvider? clock = _clock;

        builder.UseSetting("ConnectionStrings:RoomBook", _connectionString);

        if (clock is null)
        {
            return;
        }

        // ConfigureTestServices runs after the application's own registrations, which is what lets a
        // test replace the clock rather than merely add another one.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(clock);
        });
    }

    /// <summary>
    /// Removes the seeded rooms, for the one test that asks what an installation with no rooms
    /// answers. The store seeds them at startup, so reaching that state means emptying it — which is
    /// more honest than a flag that only tests ever set.
    /// </summary>
    public async Task EmptyTheRoomsAsync(CancellationToken cancellationToken)
    {
        RoomBookStore store = Services.GetRequiredService<RoomBookStore>();

        await using SqliteConnection connection = store.Connect();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM bookings; DELETE FROM rooms;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
