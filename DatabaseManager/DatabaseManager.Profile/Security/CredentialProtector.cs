#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DatabaseManager.Profile.Security;

/// <summary>
/// Protects persisted credentials with a versioned, authenticated envelope.
/// Windows uses DPAPI scoped to the current user. Other platforms use AES-GCM
/// with a random per-user key stored outside the portable Profiles directory.
/// </summary>
public sealed class CredentialProtector
{
    private const string DpapiPrefix = "dbm:v2:dpapi:";
    private const string AesGcmPrefix = "dbm:v2:aesgcm:";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static readonly byte[] AdditionalData = Encoding.UTF8.GetBytes("DatabaseManager.Profile.v2");
    private readonly string _keyFilePath;

    public static CredentialProtector Default { get; } = new();

    public CredentialProtector(string? keyDirectory = null)
    {
        var directory = keyDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DatabaseManager",
                "Security");
        }

        _keyFilePath = Path.Combine(directory, "credential.key");
    }

    public string Protect(string plainText)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainText);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var protectedBytes = ProtectedData.Protect(plainBytes, AdditionalData, DataProtectionScope.CurrentUser);
                return DpapiPrefix + Convert.ToBase64String(protectedBytes);
            }

            var key = GetOrCreateKey();
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipher = new byte[plainBytes.Length];
            var tag = new byte[TagSize];
            try
            {
                using var aes = new AesGcm(key, TagSize);
                aes.Encrypt(nonce, plainBytes, cipher, tag, AdditionalData);

                var payload = new byte[NonceSize + TagSize + cipher.Length];
                Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
                Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
                Buffer.BlockCopy(cipher, 0, payload, NonceSize + TagSize, cipher.Length);
                return AesGcmPrefix + Convert.ToBase64String(payload);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public string Unprotect(string protectedText) => Unprotect(protectedText, out _);

    public string Unprotect(string protectedText, out bool needsMigration)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectedText);
        needsMigration = false;

        if (protectedText.StartsWith(DpapiPrefix, StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("此凭据由 Windows 当前用户保护，只能由原 Windows 用户解密。");

            var payload = Convert.FromBase64String(protectedText[DpapiPrefix.Length..]);
            var plainBytes = ProtectedData.Unprotect(payload, AdditionalData, DataProtectionScope.CurrentUser);
            try
            {
                return Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }

        if (protectedText.StartsWith(AesGcmPrefix, StringComparison.Ordinal))
        {
            var payload = Convert.FromBase64String(protectedText[AesGcmPrefix.Length..]);
            if (payload.Length < NonceSize + TagSize)
                throw new CryptographicException("凭据密文格式无效。");

            var key = GetOrCreateKey();
            var plainBytes = new byte[payload.Length - NonceSize - TagSize];
            try
            {
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(
                    payload.AsSpan(0, NonceSize),
                    payload.AsSpan(NonceSize + TagSize),
                    payload.AsSpan(NonceSize, TagSize),
                    plainBytes,
                    AdditionalData);
                return Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }

        needsMigration = true;
        return UnprotectLegacy(protectedText);
    }

    public static bool IsCurrentFormat(string? value) =>
        !string.IsNullOrEmpty(value) &&
        (value.StartsWith(DpapiPrefix, StringComparison.Ordinal) ||
         value.StartsWith(AesGcmPrefix, StringComparison.Ordinal));

    private byte[] GetOrCreateKey()
    {
        var directory = Path.GetDirectoryName(_keyFilePath)!;
        Directory.CreateDirectory(directory);
        RestrictUnixPermissions(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        if (File.Exists(_keyFilePath))
            return ReadAndValidateKey();

        var key = RandomNumberGenerator.GetBytes(KeySize);
        try
        {
            using var stream = new FileStream(_keyFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(key);
            stream.Flush(flushToDisk: true);
            RestrictUnixPermissions(_keyFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return (byte[])key.Clone();
        }
        catch (IOException) when (File.Exists(_keyFilePath))
        {
            return ReadAndValidateKey();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private byte[] ReadAndValidateKey()
    {
        var key = File.ReadAllBytes(_keyFilePath);
        if (key.Length != KeySize)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new CryptographicException("本机凭据密钥无效，请恢复原 credential.key 或重新输入密码。");
        }

        return key;
    }

    private static void RestrictUnixPermissions(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, mode);
    }

    private static string UnprotectLegacy(string protectedText)
    {
        // Compatibility-only decoder. New values are never encrypted with this historical fixed material.
        var legacyKeyMaterial = Encoding.UTF8.GetBytes(string.Concat("FA5DEAAB-5171-", "405A-9CED-E2C6DED6"));
        var legacyIv = Encoding.UTF8.GetBytes(string.Concat("12345678", "12345678"));
        var cipher = Convert.FromBase64String(protectedText);
        try
        {
            using var aes = Aes.Create();
            aes.Key = legacyKeyMaterial;
            aes.IV = legacyIv;
            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            try
            {
                return Encoding.UTF8.GetString(plain);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("旧版凭据无法解密，请重新输入并保存密码。", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(legacyKeyMaterial);
            CryptographicOperations.ZeroMemory(cipher);
        }
    }
}
