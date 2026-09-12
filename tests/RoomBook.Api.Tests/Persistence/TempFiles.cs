using Microsoft.Data.Sqlite;

namespace RoomBook.Api.Tests.Persistence;

internal static class TempFiles
{
    /// <summary>
    /// Deletes a SQLite database and its companions, and shrugs if Windows still holds the handle.
    /// Cleanup is not what these tests are about: a passing assertion must not be reported as a
    /// failure because a file handle lingered for a few milliseconds, and waiting for one is
    /// forbidden by `docs/testing.md` for good reasons. The operating system clears the temp
    /// directory eventually.
    /// </summary>
    public static void DeleteBestEffort(string path)
    {
        SqliteConnection.ClearAllPools();

        foreach (string file in new[] { path, $"{path}-wal", $"{path}-shm" })
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
                // Still open somewhere; a temp file is not worth failing a green test over.
            }
        }
    }
}
