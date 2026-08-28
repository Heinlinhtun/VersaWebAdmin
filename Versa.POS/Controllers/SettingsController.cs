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
                model.PreferredPaperSize = model.PreferredPaperSize is "A4" or "A5" or "80mm"
                    ? model.PreferredPaperSize
                    : "80mm";

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
                        UseDecimalQuantity = @UseDecimalQuantity
                       WHERE SettingId = @SettingId OR SettingId > 0 LIMIT 1";

                var rowsAffected = await conn.ExecuteAsync(sql, model);

                if (rowsAffected == 0)
                {
                    string insertSql = @"INSERT INTO Settings (ShopName, Address, Phone, ReceiptFooter, TaxRate, CurrencySymbol, LogoData, LogoSize, ReceiptFontSize, PreferredPaperSize, UseDecimalQuantity)
                                 VALUES (@ShopName, @Address, @Phone, @ReceiptFooter, @TaxRate, @CurrencySymbol, @LogoData, @LogoSize, @ReceiptFontSize, @PreferredPaperSize, @UseDecimalQuantity)";
                    await conn.ExecuteAsync(insertSql, model);
                }

                return Ok(new { Message = "Settings updated with 100x100 logo", LogoData = model.LogoData });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Server Error: " + ex.Message });
            }
        }

        private static async Task EnsureSettingsSchemaAsync(MySqlConnection conn)
        {
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
        }
    }
}
