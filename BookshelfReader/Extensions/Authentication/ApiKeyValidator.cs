using System.Security.Cryptography;
using System.Text;

namespace BookshelfReader.Extensions.Authentication;

internal static class ApiKeyValidator
{
    public static bool IsMatch(string? expectedKey, string providedKey)
    {
        if (string.IsNullOrWhiteSpace(expectedKey) || string.IsNullOrEmpty(providedKey))
        {
            return false;
        }

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        byte[] providedBytes = Encoding.UTF8.GetBytes(providedKey);

        try
        {
            byte[] expectedHash = SHA256.HashData(expectedBytes);
            byte[] providedHash = SHA256.HashData(providedBytes);

            try
            {
                return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expectedHash);
                CryptographicOperations.ZeroMemory(providedHash);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedBytes);
            CryptographicOperations.ZeroMemory(providedBytes);
        }
    }

    public static string CreateKeyIdentifier(string key)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        try
        {
            byte[] hash = SHA256.HashData(keyBytes);
            try
            {
                return Convert.ToHexString(hash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}
