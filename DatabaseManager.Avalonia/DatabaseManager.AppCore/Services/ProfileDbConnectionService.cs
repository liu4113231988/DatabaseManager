using DatabaseInterpreter.Model;
using DatabaseManager.Profile.Manager;
using DatabaseManager.Profile.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 基于 <see cref="ConnectionProfileManager"/> 的连接服务实现。
/// 阶段 0 骨架：先打通「AppCore → Profile 核心库」的连接读取链路。
/// </summary>
public class ProfileDbConnectionService : IDbConnectionService
{
    public IReadOnlyList<string> GetConnectionNames()
    {
        var names = new List<string>();

        foreach (var dbType in Enum.GetValues<DatabaseType>())
        {
            if (dbType == DatabaseType.Unknown)
                continue;

            var profiles = ConnectionProfileManager.GetProfiles(dbType.ToString()).Result;
            if (profiles is null)
                continue;

            names.AddRange(profiles.Select(p => p.Name));
        }

        return names;
    }
}
