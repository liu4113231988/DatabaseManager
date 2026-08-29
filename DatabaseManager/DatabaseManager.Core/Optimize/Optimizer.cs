using Dapper;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DatabaseManager.Core
{
    public class Optimizer
    {
        private DbInterpreter dbInterpreter;
        public Optimizer(DbInterpreter dbInterpreter)
        {
            this.dbInterpreter = dbInterpreter;
        }

        public async Task<OptimizeResult> Optimize()
        {
            OptimizeResult result = new OptimizeResult();

            DatabaseType databaseType = dbInterpreter.DatabaseType;

            ConnectionInfo connectionInfo = this.dbInterpreter.ConnectionInfo;

            result.Details = new List<OptimizeResultDetail>();

            try
            {
                using (var con = this.dbInterpreter.CreateConnection())
                {
                    if (databaseType == DatabaseType.Sqlite)
                    {
                        string filePath = connectionInfo.Database;

                        string fileName = Path.GetFileName(filePath);

                        var fileInfo = new FileInfo(filePath);

                        OptimizeResultDetail detail = new OptimizeResultDetail() { ObjectType = nameof(Database), ObjectName = Path.GetFileNameWithoutExtension(fileName) };

                        detail.DataLengthBeforeOptimization = FileHelper.GetFileSizeInMB(fileInfo.Length);

                        string sql = "VACUUM";

                        await con.ExecuteAsync(sql);

                        detail.IsOK = true;

                        fileInfo = new FileInfo(filePath);

                        detail.DataLengthAfterOptimization = FileHelper.GetFileSizeInMB(fileInfo.Length);

                        result.Details.Add(detail);

                        result.IsOK = true;
                    }
                    else if (databaseType == DatabaseType.MySql)
                    {
                        Func<Task<IEnumerable<TableDataLength>>> getTableDataLengths = async () =>
                        {
                            string sql =
                               $@"SELECT TABLE_SCHEMA AS `Schema`, TABLE_NAME AS `Name`, DATA_LENGTH + INDEX_LENGTH AS `Length`
                               FROM information_schema.TABLES
                               WHERE TABLE_SCHEMA = '{connectionInfo.Database}'";

                            var results = await con.QueryAsync<TableDataLength>(sql);

                            return results;
                        };

                        IEnumerable<TableDataLength> tableDataLengths = await getTableDataLengths();

                        string sql =
                            $@"SELECT TABLE_SCHEMA AS `Schema`, TABLE_NAME AS `Name`  FROM INFORMATION_SCHEMA.`TABLES`
                               WHERE TABLE_TYPE ='BASE TABLE' AND TABLE_SCHEMA ='{connectionInfo.Database}' AND ENGINE='InnoDB'";

                        var tables = await con.QueryAsync<Table>(sql);

                        foreach (var table in tables)
                        {
                            OptimizeResultDetail detail = new OptimizeResultDetail() { ObjectType = nameof(Table), ObjectName = table.Name };

                            long length = tableDataLengths.FirstOrDefault(item => item.Name == table.Name).Length;

                            detail.DataLengthBeforeOptimization = FileHelper.GetFileSizeInMB(length);

                            try
                            {
                                sql = $"ALTER TABLE {this.dbInterpreter.GetQuotedString(table.Name)} ENGINE='InnoDB'";

                                await con.ExecuteAsync(sql);

                                detail.IsOK = true;
                            }
                            catch (Exception ex)
                            {
                                detail.Message = ex.Message;
                            }

                            result.Details.Add(detail);
                        }

                        tableDataLengths = await getTableDataLengths();

                        foreach(var detail in result.Details)
                        {
                            var tableDataLength = tableDataLengths.FirstOrDefault(item => item.Name == detail.ObjectName);

                            detail.DataLengthAfterOptimization = FileHelper.GetFileSizeInMB(tableDataLength.Length);
                        }

                        result.IsOK = true;
                    }
                    else if (databaseType == DatabaseType.Postgres)
                    {
                        // Postgres / Kingbase：执行 VACUUM（全库）。若需要可扩展为 per-table VACUUM FULL。
                        OptimizeResultDetail detail = new OptimizeResultDetail() { ObjectType = nameof(Database), ObjectName = connectionInfo.Database ?? "postgres" };
                        try
                        {
                            // 尝试获取优化前体积（可选，失败不阻断）
                            try
                            {
                                var sizeSql = $"SELECT pg_database_size('{connectionInfo.Database}') AS Length";
                                var size = await con.ExecuteScalarAsync<long?>(sizeSql);
                                if (size.HasValue) detail.DataLengthBeforeOptimization = FileHelper.GetFileSizeInMB(size.Value);
                            }
                            catch { /* ignore size query failure */ }

                            await con.ExecuteAsync("VACUUM;");
                            detail.IsOK = true;

                            try
                            {
                                var sizeSql = $"SELECT pg_database_size('{connectionInfo.Database}') AS Length";
                                var size = await con.ExecuteScalarAsync<long?>(sizeSql);
                                if (size.HasValue) detail.DataLengthAfterOptimization = FileHelper.GetFileSizeInMB(size.Value);
                            }
                            catch { /* ignore */ }
                        }
                        catch (Exception ex)
                        {
                            detail.Message = ex.Message;
                            detail.IsOK = false;
                        }
                        result.Details.Add(detail);
                        result.IsOK = result.Details.Any(d => d.IsOK);
                        if (!result.IsOK) result.Message = detail.Message;
                    }
                    else if (databaseType == DatabaseType.SqlServer)
                    {
                        // SQL Server：对每张用户表执行索引重组 + 更新统计信息
                        string tableSql = @"SELECT s.name AS [Schema], t.name AS [Name]
                                            FROM sys.tables t
                                            JOIN sys.schemas s ON s.schema_id = t.schema_id
                                            WHERE t.is_ms_shipped = 0";
                        var tables = await con.QueryAsync<Table>(tableSql);
                        foreach (var table in tables)
                        {
                            OptimizeResultDetail detail = new OptimizeResultDetail() { ObjectType = nameof(Table), ObjectName = $"{table.Schema}.{table.Name}" };
                            try
                            {
                                string quoted = $"{this.dbInterpreter.GetQuotedString(table.Schema)}.{this.dbInterpreter.GetQuotedString(table.Name)}";
                                // REORGANIZE 对在线业务更友好；失败时尝试 UPDATE STATISTICS
                                await con.ExecuteAsync($"ALTER INDEX ALL ON {quoted} REORGANIZE;");
                                try { await con.ExecuteAsync($"UPDATE STATISTICS {quoted};"); } catch { /* ignore statistics failure */ }
                                detail.IsOK = true;
                            }
                            catch (Exception ex)
                            {
                                detail.Message = ex.Message;
                                detail.IsOK = false;
                            }
                            result.Details.Add(detail);
                        }
                        result.IsOK = result.Details.Count == 0 || result.Details.Any(d => d.IsOK);
                        if (!result.IsOK) result.Message = string.Join("; ", result.Details.Where(d => !d.IsOK).Select(d => $"{d.ObjectName}: {d.Message}").Take(3));
                    }
                    else if (databaseType == DatabaseType.Oracle)
                    {
                        // Oracle：对每张表尝试 SHRINK SPACE COMPACT（需已启用 ROW MOVEMENT）并重建相关索引
                        string tableSql = @"SELECT TABLE_NAME AS Name FROM USER_TABLES";
                        var tables = await con.QueryAsync<Table>(tableSql);
                        foreach (var table in tables)
                        {
                            OptimizeResultDetail detail = new OptimizeResultDetail() { ObjectType = nameof(Table), ObjectName = table.Name };
                            try
                            {
                                string quoted = this.dbInterpreter.GetQuotedString(table.Name);
                                // 尝试启用 ROW MOVEMENT 后执行 SHRINK
                                try { await con.ExecuteAsync($"ALTER TABLE {quoted} ENABLE ROW MOVEMENT"); } catch { /* ignore */ }
                                await con.ExecuteAsync($"ALTER TABLE {quoted} SHRINK SPACE COMPACT");
                                detail.IsOK = true;
                            }
                            catch (Exception ex)
                            {
                                detail.Message = ex.Message;
                                detail.IsOK = false;
                            }
                            result.Details.Add(detail);
                        }
                        result.IsOK = result.Details.Count == 0 || result.Details.Any(d => d.IsOK);
                        if (!result.IsOK) result.Message = string.Join("; ", result.Details.Where(d => !d.IsOK).Select(d => $"{d.ObjectName}: {d.Message}").Take(3));
                    }
                    else
                    {
                        result.IsOK = false;
                        result.Message = $"当前数据库类型 {databaseType} 暂不支持自动优化。仅支持 SQLite（VACUUM）、MySQL（InnoDB 重建）、Postgres（VACUUM）、SQL Server（索引重组）与 Oracle（SHRINK COMPACT）。";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
            }

            return result;
        }
    }    
}
