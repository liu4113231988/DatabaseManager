using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Profile.Manager;
using DatabaseManager.Profile.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 基于 <see cref="ConnectionProfileManager"/> / <see cref="AccountProfileManager"/> 的连接服务实现。
/// 复用 <c>DatabaseManager.Profile</c> 与 <c>DatabaseInterpreter</c> 完成连接的增删改查与连接测试。
/// </summary>
public class ProfileDbConnectionService : IDbConnectionService
{
    private readonly IConnectionVisualService _visualService;

    public ProfileDbConnectionService(IConnectionVisualService visualService)
    {
        _visualService = visualService;
    }

    public IReadOnlyList<ConnectionItem> GetConnections()
    {
        var result = new List<ConnectionItem>();

        foreach (var dbType in DbInterpreterHelper.GetDisplayDatabaseTypes())
        {
            result.AddRange(GetConnections(dbType.ToString()));
        }

        return result;
    }

    public IReadOnlyList<ConnectionItem> GetConnections(string databaseType)
    {
        var result = new List<ConnectionItem>();

        var profiles = ConnectionProfileManager.GetProfiles(databaseType).Result;
        if (profiles is null)
            return result;

        foreach (var profile in profiles)
        {
            result.Add(ToItem(profile, databaseType));
        }

        return result;
    }

    public ConnectionItem? GetConnectionById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        var profile = ConnectionProfileManager.GetProfileById(id).Result;
        if (profile is null)
            return null;

        return ToItem(profile, profile.DatabaseType);
    }

    public async Task<IReadOnlyList<string>> TestConnectionAsync(ConnectionItem connection, CancellationToken cancellationToken = default)
    {
        var connectionInfo = new ConnectionInfo
        {
            Server = connection.Server,
            Port = connection.Port,
            ServerVersion = connection.ServerVersion,
            Database = connection.Database,
            IntegratedSecurity = connection.IntegratedSecurity,
            UserId = connection.UserId,
            Password = connection.Password,
            IsDba = connection.IsDba,
            UseSsl = connection.UseSsl,
        };

        var dbType = ParseDatabaseType(connection.DatabaseType);
        if (dbType == DatabaseType.KingbaseES)
        {
            var blockReason = KingbaseCompatibilityModes.GetConnectionBlockReason(connection.KingbaseCompatibilityMode);
            if (blockReason is not null)
                throw new NotSupportedException(blockReason);
        }

        var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, new DbInterpreterOption());

        var databases = await dbInterpreter.GetDatabasesAsync();
        return databases.Select(d => d.Name).OrderBy(n => n).ToList();
    }

    public async Task<string?> SaveAsync(ConnectionItem connection, CancellationToken cancellationToken = default)
    {
        // 需要记住密码；否则不保存明文（底层会在 rememberPassword=false 时置空）。
        bool rememberPassword = connection.RememberPassword;

        var info = new ConnectionProfileInfo
        {
            Id = connection.Id,
            AccountId = connection.AccountId,
            DatabaseType = connection.DatabaseType,
            Name = connection.Name,
            Server = connection.Server,
            Port = connection.Port,
            ServerVersion = connection.ServerVersion,
            Database = connection.Database,
            IntegratedSecurity = connection.IntegratedSecurity,
            UserId = connection.UserId,
            Password = connection.Password,
            IsDba = connection.IsDba,
            UseSsl = connection.UseSsl,
        };

        var id = await ConnectionProfileManager.Save(info, rememberPassword);
        if (!string.IsNullOrEmpty(id))
        {
            connection.Id = id;
        }

        return string.IsNullOrEmpty(id) ? null : id;
    }

    public async Task<bool> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids?.ToList() ?? new List<string>();
        if (idList.Count == 0)
            return false;

        return await ConnectionProfileManager.Delete(idList);
    }

    public async Task<bool> IsNameExistedAsync(bool isAdd, string? accountId, string name, string? id, CancellationToken cancellationToken = default)
    {
        return await ConnectionProfileManager.IsNameExisted(isAdd, accountId, name, id);
    }

    private ConnectionItem ToItem(ConnectionProfileInfo profile, string databaseType)
    {
        var item = new ConnectionItem
        {
            Id = profile.Id,
            AccountId = profile.AccountId,
            DatabaseType = string.IsNullOrEmpty(profile.DatabaseType) ? databaseType : profile.DatabaseType,
            Name = profile.Name,
            Server = profile.Server,
            Port = profile.Port,
            ServerVersion = profile.ServerVersion,
            Database = profile.Database,
            IntegratedSecurity = profile.IntegratedSecurity,
            UserId = profile.UserId,
            Password = profile.Password,
            IsDba = profile.IsDba,
            UseSsl = profile.UseSsl,
            Priority = profile.Priority,
        };

        // 合并侧车存储的分组与颜色标注。
        var visual = _visualService.Find(profile.Id);
        if (visual is not null)
        {
            item.Group = visual.Group;
            item.ColorTag = visual.ColorTag;
            item.KingbaseCompatibilityMode = visual.KingbaseCompatibilityMode;
        }

        return item;
    }

    private static DatabaseType ParseDatabaseType(string databaseType)
    {
        if (Enum.TryParse<DatabaseType>(databaseType, true, out var type))
            return type;

        return DatabaseType.Unknown;
    }
}
