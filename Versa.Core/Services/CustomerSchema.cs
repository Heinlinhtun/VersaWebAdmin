using Dapper;
using MySqlConnector;

namespace Versa.Core.Services;

public static class CustomerSchema
{
    // Run before starting a transaction: MySQL schema changes implicitly commit.
    // Used both by tenant provisioning/reset and by older running tenants.
    public static async Task EnsureOpeningBalanceAsync(MySqlConnection conn)
    {
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        var existing = (await conn.QueryAsync<string>(@"
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Customers'
  AND COLUMN_NAME IN ('OpeningBalance', 'OpeningBalancePaid')"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains("OpeningBalance"))
        {
            try { await conn.ExecuteAsync("ALTER TABLE Customers ADD COLUMN OpeningBalance DECIMAL(18,2) NOT NULL DEFAULT 0"); }
            catch (MySqlException ex) when (ex.Number == 1060) { }
        }
        if (!existing.Contains("OpeningBalancePaid"))
        {
            try { await conn.ExecuteAsync("ALTER TABLE Customers ADD COLUMN OpeningBalancePaid DECIMAL(18,2) NOT NULL DEFAULT 0"); }
            catch (MySqlException ex) when (ex.Number == 1060) { }
        }

        await conn.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS CustomerOpeningBalanceAdjustments (
    AdjustmentId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    CustomerId INT NOT NULL,
    PreviousOpeningBalance DECIMAL(18,2) NOT NULL,
    NewOpeningBalance DECIMAL(18,2) NOT NULL,
    AmountChange DECIMAL(18,2) NOT NULL,
    Reason VARCHAR(500) NOT NULL,
    ChangedByUserId INT NULL,
    ChangedAt DATETIME(6) NOT NULL,
    KEY IX_CustomerOpeningBalanceAdjustments_CustomerId (CustomerId)
) ENGINE=InnoDB");
    }
}
