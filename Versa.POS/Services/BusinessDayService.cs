using Dapper;
using MySqlConnector;

namespace Versa.POS.Services;

public readonly record struct BusinessDayRange(DateTime Start, DateTime End);
public readonly record struct BusinessDayFilter(DateTime? Start, DateTime? End);

/// <summary>Maps user-selected business dates to the shop's configured timestamp window.</summary>
public static class BusinessDayService
{
    public static async Task<BusinessDayRange> GetRangeAsync(MySqlConnection connection, DateTime fromBusinessDate, DateTime toBusinessDate)
    {
        return CreateRange(fromBusinessDate, toBusinessDate, await GetCutoffAsync(connection));
    }

    public static async Task<TimeSpan> GetCutoffAsync(MySqlConnection connection)
    {
        await EnsureSchemaAsync(connection);
        var value = await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT TIME_FORMAT(BusinessDayCutoffTime, '%H:%i:%s') FROM Settings LIMIT 1") ?? "00:00:00";
        return ParseCutoff(value);
    }

    public static BusinessDayRange CreateRange(DateTime fromBusinessDate, DateTime toBusinessDate, TimeSpan cutoff)
    {
        var first = fromBusinessDate.Date;
        var last = toBusinessDate.Date;
        if (last < first) (first, last) = (last, first);

        // Midnight uses calendar days. A custom cut-off labels the window by its closing date.
        if (cutoff == TimeSpan.Zero)
            return new BusinessDayRange(first, last.AddDays(1).AddTicks(-1));

        return new BusinessDayRange(
            first.AddDays(-1).Add(cutoff),
            last.Add(cutoff).AddTicks(-1));
    }

    public static DateTime GetCurrentBusinessDate(DateTime localNow, TimeSpan cutoff) =>
        cutoff > TimeSpan.Zero && localNow.TimeOfDay >= cutoff ? localNow.Date.AddDays(1) : localNow.Date;

    public static async Task<BusinessDayFilter> GetFilterAsync(MySqlConnection connection, string? date, string? startDate, string? endDate)
    {
        var cutoff = await GetCutoffAsync(connection);
        DateTime? start = DateTime.TryParse(startDate, out var from) ? CreateRange(from, from, cutoff).Start : null;
        DateTime? end = DateTime.TryParse(endDate, out var to) ? CreateRange(to, to, cutoff).End : null;

        if (DateTime.TryParse(date, out var selected))
        {
            var exact = CreateRange(selected, selected, cutoff);
            start = start is null || start < exact.Start ? exact.Start : start;
            end = end is null || end > exact.End ? exact.End : end;
        }

        return new BusinessDayFilter(start, end);
    }

    private static TimeSpan ParseCutoff(string value) => TimeSpan.TryParse(value, out var cutoff) && cutoff >= TimeSpan.Zero && cutoff < TimeSpan.FromDays(1)
        ? cutoff
        : TimeSpan.Zero;

    private static async Task EnsureSchemaAsync(MySqlConnection connection)
    {
        var exists = await connection.ExecuteScalarAsync<int>(@"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Settings' AND COLUMN_NAME = 'BusinessDayCutoffTime'");
        if (exists == 0)
        {
            try { await connection.ExecuteAsync("ALTER TABLE Settings ADD COLUMN BusinessDayCutoffTime TIME NOT NULL DEFAULT '00:00:00'"); }
            catch (MySqlException ex) when (ex.Number != 1060) { throw; }
        }
    }
}
