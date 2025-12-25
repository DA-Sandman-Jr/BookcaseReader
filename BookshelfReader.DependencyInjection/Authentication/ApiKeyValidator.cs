using System;
using System.Security.Cryptography;
using System.Text;

namespace BookshelfReader.DependencyInjection.Authentication;

internal static class ApiKeyValidator
{
    public static bool IsMatch(string? expectedKey, string providedKey)
    {
        if (string.IsNullOrWhiteSpace(expectedKey) || string.IsNullOrEmpty(providedKey))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        try
        {
            var expectedHash = SHA256.HashData(expectedBytes);
            var providedHash = SHA256.HashData(providedBytes);

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
        var keyBytes = Encoding.UTF8.GetBytes(key);
        try
        {
            var hash = SHA256.HashData(keyBytes);
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
