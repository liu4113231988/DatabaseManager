using System.Data;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>读取数据库属性；数据库名称使用参数，不执行调用方提供的 SQL。</summary>
public sealed class DatabasePropertiesService
{
    public static IReadOnlyList<DatabaseProperty> GetBasicProperties(ConnectionItem connection, string databaseName)
    {
        var properties = new List<DatabaseProperty>
        {
            new("连接", connection.Name),
            new("数据库类型", connection.DatabaseType),
            new("对象名称", databaseName),
        };
        var type = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
        if (ConnectionHelper.IsFileDatabase(type))
        {
            properties.Add(new("数据源", connection.Database));
            if (File.Exists(connection.Database))
                properties.Add(new("文件大小（字节）", new FileInfo(connection.Database).Length.ToString()));
        }
        else
        {
            properties.Add(new("服务器", connection.Server));
            properties.Add(new("端口", connection.Port ?? string.Empty));
            if (type is DatabaseType.Oracle or DatabaseType.DM)
                properties.Add(new("对象范围", "用户 / Schema（不是独立数据库）"));
        }
        return properties;
    }

    public async Task<IReadOnlyList<DatabaseProperty>> GetAsync(ConnectionItem connection, string databaseName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var properties = GetBasicProperties(connection, databaseName).ToList();
        var type = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
        var sql = BuildSql(type);
        if (sql is null)
        {
            properties.Add(new("信息范围", "当前类型仅显示连接或文件信息"));
            return properties;
        }
        var snapshot = connection.Clone();
        snapshot.Database = DefaultDdlService.GetMaintenanceDatabase(snapshot.DatabaseType);
        var interpreter = DbInterpreterHelper.GetDbInterpreter(type, ConnectionHelper.ToConnectionInfo(snapshot), new DbInterpreterOption());
        using var db = interpreter.CreateConnection();
        await db.OpenAsync(ct);
        using var command = db.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@database";
        parameter.DbType = DbType.String;
        parameter.Value = databaseName;
        command.Parameters.Add(parameter);
        using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("数据库不存在，或当前账号无权查看其属性。");
        for (int i = 0; i < reader.FieldCount; i++)
            properties.Add(new(reader.GetName(i), reader.IsDBNull(i) ? "不可用" : Convert.ToString(reader.GetValue(i)) ?? string.Empty));
        return properties;
    }

    internal static string? BuildSql(DatabaseType type) => type switch
    {
        DatabaseType.Postgres => """
            SELECT datname AS "数据库", pg_get_userbyid(datdba) AS "所有者",
                   pg_encoding_to_char(encoding) AS "编码", datcollate AS "排序规则", datctype AS "字符分类",
                   pg_database_size(oid) AS "数据库大小（字节）", datconnlimit AS "连接数上限（-1 表示不限）"
            FROM pg_database WHERE datname = @database
            """,
        DatabaseType.MySql => """
            SELECT SCHEMA_NAME AS `数据库`, DEFAULT_CHARACTER_SET_NAME AS `编码`, DEFAULT_COLLATION_NAME AS `排序规则`,
                   (SELECT COALESCE(SUM(COALESCE(DATA_LENGTH,0) + COALESCE(INDEX_LENGTH,0)),0)
                    FROM information_schema.TABLES WHERE TABLE_SCHEMA = @database) AS `可见表及索引大小（估算字节）`
            FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = @database
            """,
        DatabaseType.SqlServer => """
            SELECT name AS [数据库], SUSER_SNAME(owner_sid) AS [所有者], collation_name AS [排序规则],
                   state_desc AS [状态], recovery_model_desc AS [恢复模式], create_date AS [创建时间],
                   (SELECT SUM(CONVERT(bigint,size))*8192 FROM sys.master_files WHERE database_id = d.database_id) AS [可见数据库文件大小（字节）]
            FROM sys.databases AS d WHERE name = @database
            """,
        _ => null,
    };
}
