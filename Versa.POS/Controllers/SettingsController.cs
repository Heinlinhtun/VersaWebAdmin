using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Versa.Core.Models;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : BaseShopController
    {
        private readonly DatabaseService _dbService;
        private readonly IWebHostEnvironment _env; // ✅ File သိမ်းရန် Environment လိုအပ်သည်

        public SettingsController(DatabaseService dbService, IWebHostEnvironment env)
        {
            _dbService = dbService;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await EnsureSettingsSchemaAsync(conn);
            var settings = await conn.QueryFirstOrDefaultAsync("SELECT * FROM Settings LIMIT 1");
            return Ok(settings);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromForm] Setting model, IFormFile? logo)
        {
            try
            {
                using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
                await EnsureSettingsSchemaAsync(conn);
                if (model.QuantityDecimalPlaces < 1 || model.QuantityDecimalPlaces > 5)
                    return BadRequest(new { Message = "Quantity decimal places must be between 1 and 5." });
                if (model.UseDecimalQuantity) await EnsureQuantityPrecisionAsync(conn);
                model.PreferredPaperSize = model.PreferredPaperSize is "A4" or "A5" or "80mm"
                    ? model.PreferredPaperSize
                    : "80mm";
                if (model.BusinessDayCutoffTime < TimeSpan.Zero || model.BusinessDayCutoffTime >= TimeSpan.FromDays(1))
                    return BadRequest(new { Message = "Business-day cut-off time must be between 00:00 and 23:59." });

                // 1. Logo Resize & Binary Logic
                if (logo != null)
                {
                    using (var inputStream = logo.OpenReadStream())
                    using (var outputStream = new MemoryStream())
                    {
                        using (Image image = Image.Load(inputStream))
                        {
                            // ပုံကို 100x100 အဖြစ် ချုံ့ခြင်း
                            image.Mutate(x => x.Resize(new ResizeOptions
                            {
                                Size = new Size(100, 100),
                                Mode = ResizeMode.Max // Aspect Ratio ကို ထိန်းပြီး အကြီးဆုံး 100 ထားမည်
                            }));

                            // JPEG အဖြစ်ပြောင်းပြီး Quality 80% ဖြင့် သိမ်းခြင်း
                            image.SaveAsJpeg(outputStream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 80 });

                            byte[] fileBytes = outputStream.ToArray();
                            model.LogoData = $"data:image/jpeg;base64,{Convert.ToBase64String(fileBytes)}";
                        }
                    }
                }

                // 2. Update SQL
                string sql = @"UPDATE Settings SET 
                        ShopName = @ShopName, Address = @Address, Phone = @Phone, 
                        ReceiptFooter = @ReceiptFooter, TaxRate = @TaxRate, 
                        CurrencySymbol = @CurrencySymbol,
                        LogoData = @LogoData,
                        LogoSize = @LogoSize,
                        ReceiptFontSize = @ReceiptFontSize,
                        PreferredPaperSize = @PreferredPaperSize,
                        UseDecimalQuantity = @UseDecimalQuantity, QuantityDecimalPlaces = @QuantityDecimalPlaces,
                        BusinessDayCutoffTime = @BusinessDayCutoffTime
                       WHERE SettingId = @SettingId OR SettingId > 0 LIMIT 1";

                var rowsAffected = await conn.ExecuteAsync(sql, model);

                if (rowsAffected == 0 && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Settings") == 0)
                {
                    string insertSql = @"INSERT INTO Settings (ShopName, Address, Phone, ReceiptFooter, TaxRate, CurrencySymbol, LogoData, LogoSize, ReceiptFontSize, PreferredPaperSize, UseDecimalQuantity, QuantityDecimalPlaces, BusinessDayCutoffTime)
                                 VALUES (@ShopName, @Address, @Phone, @ReceiptFooter, @TaxRate, @CurrencySymbol, @LogoData, @LogoSize, @ReceiptFontSize, @PreferredPaperSize, @UseDecimalQuantity, @QuantityDecimalPlaces, @BusinessDayCutoffTime)";
                    await conn.ExecuteAsync(insertSql, model);
                }

                return Ok(new { Message = "Settings updated with 100x100 logo", LogoData = model.LogoData });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Server Error: " + ex.Message });
            }
        }

        // Widen quantity storage before enabling fine precision. Never narrow existing
        // scale or integer capacity, and leave money columns untouched.
        private static async Task EnsureQuantityPrecisionAsync(MySqlConnection conn)
        {
            var columns = await conn.QueryAsync(@"
                SELECT TABLE_NAME AS TableName, COLUMN_NAME AS ColumnName,
                       NUMERIC_PRECISION AS NumPrecision, NUMERIC_SCALE AS NumScale,
                       IS_NULLABLE AS IsNullable, COLUMN_DEFAULT AS DefaultValue,
                       COLUMN_TYPE AS ColumnType
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND DATA_TYPE IN ('decimal', 'int', 'bigint', 'smallint', 'mediumint', 'tinyint')
                  AND COALESCE(NUMERIC_SCALE, 0) < 5
                  AND EXTRA NOT LIKE '%GENERATED%'
                  AND COLUMN_NAME IN ('Quantity', 'BaseQuantity', 'FOCQuantity', 'RemainingQuantity',
                      'OriginalBaseQuantity', 'Multiplier', 'UnitMultiplier', 'MinQuantity',
                      'AlertQuantity', 'SoldQuantity', 'SettledQuantity', 'ReturnedQuantity', 'TotalBaseQuantity',
                      'PreviousQuantity', 'ImportedQuantity', 'PostImportQuantity')");
            foreach (var column in columns)
            {
                string table = ((string)column.TableName).Replace("`", "``");
                string name = ((string)column.ColumnName).Replace("`", "``");
                int integerDigits = Convert.ToInt32(column.NumPrecision) - Convert.ToInt32(column.NumScale ?? 0);
                int precision = Math.Max(18, integerDigits + 5);
                string nullable = (string)column.IsNullable == "YES" ? "NULL" : "NOT NULL";
                string unsigned = ((string)column.ColumnType).Contains("unsigned") ? " UNSIGNED" : "";
                string? defaultValue = column.DefaultValue?.ToString();
                string defaultSql = defaultValue == null || defaultValue.Equals("NULL", StringComparison.OrdinalIgnoreCase) ? "" : " DEFAULT " +
                    Convert.ToDecimal(defaultValue.Trim('\''), System.Globalization.CultureInfo.InvariantCulture)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                await conn.ExecuteAsync($"ALTER TABLE `{table}` MODIFY COLUMN `{name}` DECIMAL({precision},5){unsigned} {nullable}{defaultSql}");
            }
        }

        private static async Task EnsureSettingsSchemaAsync(MySqlConnection conn)
        {
            var precisionExists = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Settings' AND COLUMN_NAME = 'QuantityDecimalPlaces'");
            if (precisionExists == 0)
            {
                try { await conn.ExecuteAsync("ALTER TABLE Settings ADD COLUMN QuantityDecimalPlaces INT NOT NULL DEFAULT 2"); }
                catch (MySqlException ex) when (ex.Number == 1060) { }
            }
            var useDecimalExists = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'Settings'
                  AND COLUMN_NAME = 'UseDecimalQuantity'");

            if (useDecimalExists == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE Settings ADD COLUMN UseDecimalQuantity TINYINT(1) NOT NULL DEFAULT 0");
            }

            var receiptFontSizeExists = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'Settings'
                  AND COLUMN_NAME = 'ReceiptFontSize'");

            if (receiptFontSizeExists == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE Settings ADD COLUMN ReceiptFontSize VARCHAR(10) NOT NULL DEFAULT 'Medium'");
            }

            var preferredPaperSizeExists = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'Settings'
                  AND COLUMN_NAME = 'PreferredPaperSize'");

            if (preferredPaperSizeExists == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE Settings ADD COLUMN PreferredPaperSize VARCHAR(10) NOT NULL DEFAULT '80mm'");
            }

            var cutoffExists = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Settings' AND COLUMN_NAME = 'BusinessDayCutoffTime'");
            if (cutoffExists == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE Settings ADD COLUMN BusinessDayCutoffTime TIME NOT NULL DEFAULT '00:00:00'");
            }
        }
    }
}
