using Dapper;
using MySqlConnector;

namespace Versa.Core.Services;

public static class SalesChannels
{
    public static bool IsValid(string? value) => value is null or "" or "Offline" or "Facebook" or "TikTok" or "Unspecified";

    // NULL preserves the meaning of sales made before channels were introduced.
    // Call outside transactions because MySQL ALTER TABLE implicitly commits.
    public static async Task EnsureAsync(MySqlConnection conn)
    {
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        var exists = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Sales' AND COLUMN_NAME = 'SalesChannel'");
        if (exists == 0)
        {
            try { await conn.ExecuteAsync("ALTER TABLE Sales ADD COLUMN SalesChannel VARCHAR(20) NULL DEFAULT NULL"); }
            catch (MySqlException ex) when (ex.Number == 1060) { }
        }
    }
}
