using System.Globalization;

namespace RoomBook.Api.Bookings;

/// <summary>
/// Reads query-string values the way this API has decided to read them: a value is present or it is
/// not, a number is whole, an instant is UTC (BR-5), an identifier is a GUID. Shared by every
/// endpoint that takes a query, so two of them cannot come to refuse a malformed value differently.
/// </summary>
internal static class QueryValues
{
    public static bool Has(IQueryCollection query, string name) => query.ContainsKey(name);

    public static bool TryInt(IQueryCollection query, string name, out int? value)
    {
        value = null;

        if (!query.ContainsKey(name)
            || !int.TryParse(query[name], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return false;
        }

        value = parsed;

        return true;
    }

    public static bool TryGuid(IQueryCollection query, string name, out Guid? value)
    {
        value = null;

        if (!query.ContainsKey(name) || !Guid.TryParse(query[name], out Guid parsed))
        {
            return false;
        }

        value = parsed;

        return true;
    }

    public static bool TryInstant(IQueryCollection query, string name, out DateTimeOffset? value)
    {
        value = null;

        if (!query.ContainsKey(name)
            || !DateTimeOffset.TryParse(
                query[name],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset parsed))
        {
            return false;
        }

        // BR-5: the wire carries UTC instants, not local times with an offset.
        if (parsed.Offset != TimeSpan.Zero)
        {
            return false;
        }

        value = parsed;

        return true;
    }
}
