using System.Security.Cryptography;
using System.Text;

namespace Versa.Core.Services;

public class EncryptionService
{
    private const string FixedKey = "acrdpZTYNGsol1ha";

    public string EncryptMacAddress(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return string.Empty;

        try
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(FixedKey);
                aes.IV = new byte[16]; // Same fixed IV (all zeros)

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(plainText);
                        }
                    }

                    byte[] encryptedBytes = ms.ToArray();

                    // Convert byte[] → HEX string
                    StringBuilder hex = new StringBuilder(encryptedBytes.Length * 2);
                    foreach (byte b in encryptedBytes)
                    {
                        hex.AppendFormat("{0:X2}", b);
                    }

                    return hex.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            return $"Encryption failed: {ex.Message}";
        }
    }
}
