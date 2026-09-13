using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>Import-only cleanup also removes accounts created for the imported profiles.</summary>
public interface IConnectionImportRollback
{
    Task RollbackImportAsync(IReadOnlyList<string> ids);
}

public sealed class ConnectionTransferEntry
{
    public ConnectionItem Connection { get; set; } = new();
    public string? SshSecret { get; set; }
}
public sealed class ConnectionTransferEnvelope
{
    public int Version { get; set; } = 1;
    public bool Encrypted { get; set; }
    public string Salt { get; set; } = "";
    public string Nonce { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Payload { get; set; } = "";
}
public static class ConnectionTransferService
{
    private const int MaxBytes = 4 * 1024 * 1024;
    public static string Export(IReadOnlyList<ConnectionItem> connections, bool includePasswords, string password)
    {
        if (connections.Count is < 1 or > 200) throw new ArgumentException("请选择 1–200 个连接。");
        if (includePasswords && password.Length < 12) throw new ArgumentException("携带密码时必须设置至少 12 位的导出保护口令。");
        var entries = connections.Select(c =>
        {
            var copy = JsonConvert.DeserializeObject<ConnectionItem>(JsonConvert.SerializeObject(c))!;
            copy.Id = null; copy.AccountId = null; copy.RememberPassword = includePasswords;
            if (!includePasswords) copy.Password = null;
            if (copy.Ssh is not null) copy.Ssh.EncryptedSecret = "";
            return new ConnectionTransferEntry { Connection = copy, SshSecret = includePasswords ? c.Ssh?.Secret : null };
        }).ToArray();
        byte[] payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entries));
        if (payload.Length > MaxBytes) throw new ArgumentException("连接配置过大。");
        var envelope = new ConnectionTransferEnvelope { Encrypted = includePasswords };
        if (includePasswords)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16), nonce = RandomNumberGenerator.GetBytes(12), tag = new byte[16], cipher = new byte[payload.Length];
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);
            try { using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, payload, cipher, tag, "DatabaseManager-connections-v1"u8); }
            finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(payload); }
            envelope.Salt = Convert.ToBase64String(salt); envelope.Nonce = Convert.ToBase64String(nonce); envelope.Tag = Convert.ToBase64String(tag); payload = cipher;
        }
        envelope.Payload = Convert.ToBase64String(payload);
        return JsonConvert.SerializeObject(envelope, Formatting.Indented);
    }
    public static IReadOnlyList<ConnectionItem> Preview(string json, string password)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes * 2) throw new ArgumentException("连接文件超过大小限制。");
        var envelope = JsonConvert.DeserializeObject<ConnectionTransferEnvelope>(json) ?? throw new ArgumentException("连接文件无效。");
        if (envelope.Version != 1) throw new ArgumentException("不支持的连接文件版本。");
        byte[] data = Convert.FromBase64String(envelope.Payload);
        if (envelope.Encrypted)
        {
            byte[] salt = Convert.FromBase64String(envelope.Salt), nonce = Convert.FromBase64String(envelope.Nonce), tag = Convert.FromBase64String(envelope.Tag);
            if (salt.Length != 16 || nonce.Length != 12 || tag.Length != 16) throw new ArgumentException("加密参数无效。");
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32), plain = new byte[data.Length];
            try { using var aes = new AesGcm(key, 16); aes.Decrypt(nonce, data, tag, plain, "DatabaseManager-connections-v1"u8); }
            catch (CryptographicException) { throw new ArgumentException("保护口令错误或文件已被修改。"); }
            finally { CryptographicOperations.ZeroMemory(key); }
            data = plain;
        }
        try
        {
            var entries = JsonConvert.DeserializeObject<List<ConnectionTransferEntry>>(Encoding.UTF8.GetString(data)) ?? throw new ArgumentException("连接内容无效。");
            if (entries.Count is < 1 or > 200) throw new ArgumentException("连接数量无效。");
            foreach (var entry in entries)
            {
                var c = entry.Connection ?? throw new ArgumentException("连接为空。");
                if (ConnectionHelper.ParseDatabaseType(c.DatabaseType) == DatabaseInterpreter.Model.DatabaseType.Unknown || string.IsNullOrWhiteSpace(c.Name)) throw new ArgumentException("连接类型或名称无效。");
                c.Id = null; c.AccountId = null;
                if (!envelope.Encrypted && (!string.IsNullOrEmpty(c.Password) || !string.IsNullOrEmpty(entry.SshSecret))) throw new ArgumentException("拒绝未加密文件中的密码。");
                c.RememberPassword = envelope.Encrypted;
                if (c.Ssh is not null) { c.Ssh.EncryptedSecret = ""; c.Ssh.Secret = envelope.Encrypted ? entry.SshSecret ?? "" : ""; }
            }
            return entries.Select(e => e.Connection).ToArray();
        }
        finally { CryptographicOperations.ZeroMemory(data); }
    }
    public static async Task<int> ImportAsync(IDbConnectionService service, IReadOnlyList<ConnectionItem> selected, bool renameConflicts, CancellationToken ct)
    {
        var names = service.GetConnections().Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var saved = new List<string>();
        try
        {
            foreach (var item in selected)
            {
                ct.ThrowIfCancellationRequested();
                var copy = JsonConvert.DeserializeObject<ConnectionItem>(JsonConvert.SerializeObject(item))!;
                copy.Ssh = item.Ssh; copy.Id = null; copy.AccountId = null;
                string original = copy.Name; int suffix = 1;
                if (names.Contains(copy.Name) && !renameConflicts) continue;
                while (!names.Add(copy.Name)) copy.Name = $"{original} (导入 {suffix++})";
                try { saved.Add(await service.SaveAsync(copy, ct) ?? throw new InvalidOperationException("保存连接失败。")); }
                catch
                {
                    // A profile may already have committed before writing its SSH/visual sidecars fails.
                    if (!string.IsNullOrEmpty(copy.Id) && !saved.Contains(copy.Id)) saved.Add(copy.Id);
                    throw;
                }
            }
            return saved.Count;
        }
        catch (Exception failure)
        {
            try
            {
                if (saved.Count > 0)
                {
                    if (service is IConnectionImportRollback rollback) await rollback.RollbackImportAsync(saved);
                    else if (!await service.DeleteAsync(saved, CancellationToken.None)) throw new InvalidOperationException("无法清理已创建的连接。");
                }
            }
            catch (Exception cleanup) { throw new InvalidOperationException("导入失败且清理未完成，请在连接管理中检查本批新增连接。", new AggregateException(failure, cleanup)); }
            throw;
        }
    }
}
