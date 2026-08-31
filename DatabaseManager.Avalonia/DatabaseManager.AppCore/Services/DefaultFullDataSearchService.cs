using System.Data;
using System.Diagnostics;
using System.Text;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 全库数据搜索实现：
/// - 枚举表/视图（经 <see cref="DbInterpreter"/>，遵循 Schema 过滤）；
/// - 按方言拼接引用标识符与 LIKE 转义，文本类列拼 OR 条件，每表限制返回行数；
/// - 命中行再在客户端按列确认并生成预览与定位条件。
/// </summary>
public class DefaultFullDataSearchService : IFullDataSearchService
{
    /// <summary>文本类数据类型（小写包含匹配）。</summary>
    private static readonly string[] TextTypes =
    {
        "char", "text", "clob", "string", "uuid", "uniqueidentifier", "json", "xml",
        "enum", "citext", "long", "interval",
    };

    public async Task<FullDataSearchResult> SearchAsync(
        ConnectionItem connection,
        string keyword,
        FullDataSearchOptions? options = null,
        Action<string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new FullDataSearchOptions();
        var result = new FullDataSearchResult();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            result.Error = "搜索关键字不能为空。";
            return result;
        }

        var dbType = ParseDatabaseType(connection.DatabaseType);
        if (dbType == DatabaseType.Unknown)
        {
            result.Error = $"不支持的数据库类型：{connection.DatabaseType}。";
            return result;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var interpreter = CreateInterpreter(connection, options.Database);

            await using var conn = interpreter.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            // 1) 枚举目标对象（表 + 可选视图）。
            var filter = new SchemaInfoFilter
            {
                DatabaseObjectType = DatabaseObjectType.Table,
                Schema = options.Schema,
            };

            var objects = new List<(string Name, string? Schema, bool IsView)>();

            var tables = await interpreter.GetTablesAsync(conn, filter);
            foreach (var t in tables)
            {
                objects.Add((t.Name, t.Schema, false));
            }

            if (options.IncludeViews)
            {
                var views = await interpreter.GetViewsAsync(conn, filter);
                foreach (var v in views)
                {
                    objects.Add((v.Name, v.Schema, true));
                }
            }

            if (objects.Count > options.MaxTables)
            {
                objects = objects.Take(options.MaxTables).ToList();
            }

            onProgress?.Invoke($"共 {objects.Count} 个对象待扫描。");

            // 2) 逐表搜索。
            int index = 0;
            foreach (var (name, schema, isView) in objects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;

                var tableResult = new FullDataSearchTableResult { TableName = name, Schema = schema ?? string.Empty };
                result.Tables.Add(tableResult);
                result.ScannedTables++;

                onProgress?.Invoke($"({index}/{objects.Count}) 正在扫描 {tableResult.DisplayName} ...");

                try
                {
                    var columns = await GetSearchableColumnsAsync(
                        interpreter, conn, dbType, name, schema, options, cancellationToken);

                    if (columns.Count == 0)
                    {
                        continue;
                    }

                    var sql = BuildSearchSql(dbType, tableResult.DisplayName, columns, keyword, options.MaxMatchesPerTable);
                    var rows = await ExecuteQueryAsync(conn, sql, options.CommandTimeoutSeconds, cancellationToken);

                    foreach (var row in rows)
                    {
                        // 客户端逐列确认命中，产出预览与定位条件。
                        var matched = new List<string>();
                        var preview = new StringBuilder();

                        foreach (var (colName, colValue) in row)
                        {
                            if (colValue is not null
                                && colValue.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            {
                                matched.Add(colName);

                                if (preview.Length > 0)
                                {
                                    preview.Append("; ");
                                }

                                if (preview.Length < 200)
                                {
                                    var text = colValue.Length > 60 ? colValue[..60] + "…" : colValue;
                                    preview.Append($"{colName}={text}");
                                }

                                tableResult.MatchedColumns.Add(colName);
                            }
                        }

                        if (matched.Count == 0)
                        {
                            continue;
                        }

                        var searchRow = new FullDataSearchRow { Preview = preview.ToString() };
                        foreach (var col in matched.Distinct())
                        {
                            var value = row.FirstOrDefault(kv => kv.Key == col).Value ?? string.Empty;
                            searchRow.Conditions.Add(new KeyValuePair<string, string>(col, value));
                        }

                        tableResult.Rows.Add(searchRow);
                        result.TotalMatches++;

                        if (tableResult.Rows.Count >= options.MaxMatchesPerTable)
                        {
                            break;
                        }
                    }

                    if (tableResult.Rows.Count > 0)
                    {
                        result.MatchedTables++;
                        onProgress?.Invoke($"已命中 {tableResult.DisplayName}（{tableResult.Rows.Count} 行）。");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 单表错误（权限/超时/类型不支持）不中断整体。
                    tableResult.Error = ex.Message;
                }
            }

            onProgress?.Invoke(
                $"扫描完成：{result.ScannedTables} 个对象，命中 {result.MatchedTables} 个对象 / {result.TotalMatches} 行。");
        }
        catch (OperationCanceledException)
        {
            result.Error = "搜索已取消。";
        }
        catch (Exception ex)
        {
            result.Error = $"搜索失败：{ex.Message}";
        }
        finally
        {
            sw.Stop();
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
        }

        return result;
    }

    /// <summary>获取参与搜索的列（文本类列或全部列）。</summary>
    private async Task<List<string>> GetSearchableColumnsAsync(
        DbInterpreter interpreter,
        System.Data.Common.DbConnection conn,
        DatabaseType dbType,
        string tableName,
        string? schema,
        FullDataSearchOptions options,
        CancellationToken cancellationToken)
    {
        var filter = new SchemaInfoFilter
        {
            DatabaseObjectType = DatabaseObjectType.Table,
            Schema = schema,
            TableNames = new[] { tableName },
        };

        var columns = await interpreter.GetTableColumnsAsync(conn, filter);
        var names = new List<string>();

        foreach (var col in columns)
        {
            if (col.IsComputed)
            {
                continue;
            }

            if (options.TextColumnsOnly && !IsTextType(col.DataType))
            {
                continue;
            }

            names.Add(col.Name);
        }

        return names;
    }

    private static bool IsTextType(string? dataType)
    {
        if (string.IsNullOrEmpty(dataType))
        {
            return false;
        }

        var lower = dataType.ToLowerInvariant();
        return TextTypes.Any(t => lower.Contains(t));
    }

    /// <summary>按方言构造带 LIKE 条件与行数限制的搜索 SQL。</summary>
    internal static string BuildSearchSql(
        DatabaseType dbType, string quotedTable, List<string> columnNames, string keyword, int limit)
    {
        string escaped = EscapeLike(dbType, keyword);
        string likeSuffix = dbType == DatabaseType.SqlServer ? string.Empty : " ESCAPE '\\'";
        var conditions = columnNames
            .Select(c => $"{SqlDialectHelper.QuoteIdentifier(dbType, c)} LIKE {QuoteValue(dbType, $"%{escaped}%")}{likeSuffix}");
        string where = string.Join(" OR ", conditions);

        return dbType switch
        {
            DatabaseType.SqlServer => $"SELECT TOP {limit} * FROM {SqlDialectHelper.QuoteQualifiedIdentifier(dbType, quotedTable)} WHERE {where}",
            DatabaseType.Oracle => $"SELECT * FROM {SqlDialectHelper.QuoteQualifiedIdentifier(dbType, quotedTable)} WHERE ({where}) AND ROWNUM <= {limit}",
            _ => $"SELECT * FROM {SqlDialectHelper.QuoteQualifiedIdentifier(dbType, quotedTable)} WHERE {where} LIMIT {limit}",
        };
    }

    private static async Task<List<List<KeyValuePair<string, string>>>> ExecuteQueryAsync(
        System.Data.Common.DbConnection conn, string sql, int timeoutSeconds, CancellationToken ct)
    {
        var rows = new List<List<KeyValuePair<string, string>>>();

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = Math.Clamp(timeoutSeconds, 1, 300);
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var columnNames = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();

        while (await reader.ReadAsync(ct))
        {
            var row = new List<KeyValuePair<string, string>>(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
                row.Add(new KeyValuePair<string, string>(columnNames[i], value ?? string.Empty));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string QuoteValue(DatabaseType dbType, string value)
    {
        _ = dbType;
        return "'" + value.Replace("'", "''") + "'";
    }

    private static string EscapeLike(DatabaseType dbType, string keyword) => dbType switch
    {
        // SQL Server 用方括号转义通配符；其余用反斜杠（配合 ESCAPE '\\'）。
        DatabaseType.SqlServer => keyword.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]"),
        _ => keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_"),
    };

    private static DbInterpreter CreateInterpreter(ConnectionItem connection, string? databaseOverride)
    {
        var connectionInfo = new ConnectionInfo
        {
            Server = connection.Server,
            Port = connection.Port,
            ServerVersion = connection.ServerVersion,
            Database = string.IsNullOrEmpty(databaseOverride) ? connection.Database : databaseOverride,
            IntegratedSecurity = connection.IntegratedSecurity,
            UserId = connection.UserId,
            Password = connection.Password,
            IsDba = connection.IsDba,
            UseSsl = connection.UseSsl,
        };

        var option = new DbInterpreterOption
        {
            ObjectFetchMode = DatabaseObjectFetchMode.Simple,
        };

        return DbInterpreterHelper.GetDbInterpreter(ParseDatabaseType(connection.DatabaseType), connectionInfo, option);
    }

    private static DatabaseType ParseDatabaseType(string databaseType)
        => Enum.TryParse<DatabaseType>(databaseType, true, out var type) ? type : DatabaseType.Unknown;
}
