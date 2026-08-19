using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 查询服务实现。阶段 0 仅建立骨架，后续阶段接入完整查询链路。
/// </summary>
public class DefaultQueryService : IQueryService
{
    public Task<string> ExecuteAsync(string connectionName, string sql, CancellationToken cancellationToken = default)
    {
        // TODO(阶段 2)：接入 DbInterpreter 执行查询并返回结果集。
        return Task.FromResult($"Query queued for connection '{connectionName}'. (phase 0 skeleton)");
    }
}
