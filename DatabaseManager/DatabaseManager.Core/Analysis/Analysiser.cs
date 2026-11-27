using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseManager.Core
{
    public class Analysiser
    {
        private DbInterpreter dbInterpreter;

        public Analysiser(DbInterpreter dbInterpreter)
        {
            this.dbInterpreter = dbInterpreter;
        }

        public async Task<IEnumerable<IndexFragmentation>> GetIndexFragmentations()
        {
            DatabaseType databaseType = dbInterpreter.DatabaseType;

            using (var con = this.dbInterpreter.CreateConnection())
            {
                return databaseType switch
                {
                    DatabaseType.SqlServer => await this.GetSqlServerFragmentationsAsync(con),
                    DatabaseType.Postgres or DatabaseType.KingbaseES => await this.GetPostgresStyleFragmentationsAsync(con),
                    DatabaseType.Oracle or DatabaseType.DM => await this.GetOracleStyleFragmentationsAsync(con),
                    DatabaseType.MySql => await this.GetMySqlFragmentationsAsync(con),
                    DatabaseType.Sqlite => await this.GetSqliteFragmentationsAsync(con),
                    // DuckDB 为列式存储，无传统 B-Tree 索引碎片统计概念，返回空集合避免误报。
                    DatabaseType.DuckDB => Enumerable.Empty<IndexFragmentation>(),
                    _ => throw new NotSupportedException($"{databaseType} 暂不支持索引碎片分析。")
                };
            }
        }

        private async Task<IEnumerable<IndexFragmentation>> GetSqlServerFragmentationsAsync(DbConnection con)
        {
            const string sql = @"SELECT SCHEMA_NAME(t.schema_id) AS [Schema],OBJECT_NAME(i.OBJECT_ID) AS TableName,  i.name AS IndexName,ROUND(avg_fragmentation_in_percent,2) AS FragmentationPercent
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, NULL) indexstats
INNER JOIN sys.indexes i 
JOIN sys.tables t ON i.object_id=t.object_id
ON i.object_id = indexstats.object_id 
AND i.index_id = indexstats.index_id
WHERE avg_fragmentation_in_percent>0";

            return await DbRowMapper.ReadObjectsAsync<IndexFragmentation>(con, sql);
        }

        // KingbaseES 为 PG 兼容模式，复用 pgstatindex 路径。
        private async Task<IEnumerable<IndexFragmentation>> GetPostgresStyleFragmentationsAsync(DbConnection con)
        {
            var tableNames = (await this.dbInterpreter.GetTablesAsync(con)).Select(item => item.Name).ToArray();
            var indexes = await this.dbInterpreter.GetTableIndexesAsync(con, new SchemaInfoFilter() { TableNames = tableNames }, false);

            StringBuilder sb = new StringBuilder();

            int i = 0;
            foreach (var index in indexes)
            {
                sb.AppendLine($@"SELECT '{index.Schema}' AS ""Schema"",'{index.TableName}' AS ""TableName"", '{index.Name}' AS ""IndexName"", leaf_fragmentation AS ""FragmentationPercent"" FROM pgstatindex('{index.Name}') WHERE leaf_fragmentation>0 and leaf_fragmentation<>'NaN'");

                if (i < indexes.Count - 1)
                {
                    sb.AppendLine("UNION ALL");
                }

                i++;
            }

            return await DbRowMapper.ReadObjectsAsync<IndexFragmentation>(con, sb.ToString());
        }

        // DM 为 Oracle 兼容方言，复用 ANALYZE INDEX ... VALIDATE STRUCTURE + INDEX_STATS 路径。
        private async Task<IEnumerable<IndexFragmentation>> GetOracleStyleFragmentationsAsync(DbConnection con)
        {
            var tableNames = (await this.dbInterpreter.GetTablesAsync(con)).Select(item => item.Name).ToArray();
            var indexes = await this.dbInterpreter.GetTableIndexesAsync(con, new SchemaInfoFilter() { TableNames = tableNames }, false);

            foreach (var index in indexes)
            {
                string execSql = $"analyze index {this.dbInterpreter.GetQuotedDbObjectNameWithSchema(index)} validate structure";

                await DbRowMapper.ExecuteNonQueryAsync(con, execSql);
            }

            string sql =
$@"SELECT I.TABLE_OWNER AS ""Schema"",I.TABLE_NAME AS ""TableName"",S.NAME AS ""IndexName"",
ROUND(del_lf_rows_len*100.0/lf_rows_len,2) AS ""FragmentationPercent""
FROM sys.INDEX_STATS S
JOIN USER_INDEXES I ON S.NAME=I.INDEX_NAME
WHERE I.TABLE_OWNER='{this.dbInterpreter.ConnectionInfo.Database}' AND del_lf_rows_len*100.0/lf_rows_len>0";

            return await DbRowMapper.ReadObjectsAsync<IndexFragmentation>(con, sql);
        }

        // MySQL 不暴露逐索引碎片率，这里用表级 DATA_FREE 占用比作为碎片指标（逐索引行展示，重建时按表 OPTIMIZE）。
        private async Task<IEnumerable<IndexFragmentation>> GetMySqlFragmentationsAsync(DbConnection con)
        {
            const string sql = @"SELECT
  t.TABLE_SCHEMA AS `Schema`,
  t.TABLE_NAME AS `TableName`,
  s.INDEX_NAME AS `IndexName`,
  ROUND(t.DATA_FREE * 100.0 / NULLIF(t.DATA_LENGTH + t.INDEX_LENGTH, 0), 2) AS `FragmentationPercent`
FROM information_schema.TABLES t
JOIN information_schema.STATISTICS s
  ON s.TABLE_SCHEMA = t.TABLE_SCHEMA AND s.TABLE_NAME = t.TABLE_NAME
WHERE t.TABLE_SCHEMA = DATABASE()
  AND t.DATA_FREE > 0
  AND (t.DATA_LENGTH + t.INDEX_LENGTH) > 0";

            return await DbRowMapper.ReadObjectsAsync<IndexFragmentation>(con, sql);
        }

        // SQLite 无索引碎片统计，用文件空闲页占比（freelist_count / page_count）作为 VACUUM 回收指标。
        private async Task<IEnumerable<IndexFragmentation>> GetSqliteFragmentationsAsync(DbConnection con)
        {
            long pageCount = Convert.ToInt64(await this.dbInterpreter.GetScalarAsync(con, "PRAGMA page_count"));
            if (pageCount <= 0)
            {
                return Enumerable.Empty<IndexFragmentation>();
            }

            long freelist = Convert.ToInt64(await this.dbInterpreter.GetScalarAsync(con, "PRAGMA freelist_count"));
            double ratio = freelist * 100.0 / pageCount;
            if (ratio <= 0)
            {
                return Enumerable.Empty<IndexFragmentation>();
            }

            string dbFile = this.dbInterpreter.ConnectionInfo.Database;
            string tableName = string.IsNullOrEmpty(dbFile) ? "(database)" : Path.GetFileName(dbFile);

            return new List<IndexFragmentation>
            {
                new IndexFragmentation
                {
                    Schema = "main",
                    TableName = tableName,
                    IndexName = "(文件空闲空间)",
                    FragmentationPercent = Math.Round(ratio, 2).ToString()
                }
            };
        }

        public async Task<OperateResult> RebuildIndex(IndexFragmentation indexFragmentation)
        {
            OperateResult result = new OperateResult();

            TableIndex index = new TableIndex() { Schema = indexFragmentation.Schema, TableName = indexFragmentation.TableName, Name = indexFragmentation.IndexName };

            try
            {
                DatabaseType databaseType = dbInterpreter.DatabaseType;

                string sql = this.GetRebuildIndexSql(databaseType, index);

                if (string.IsNullOrEmpty(sql))
                {
                    result.IsOK = false;
                    result.Message = $"{databaseType} 无需重建索引。";
                    return result;
                }

                sql = sql.Trim(' ', ';');

                using (var con = this.dbInterpreter.CreateConnection())
                {
                    await con.OpenAsync();

                    // SQLite 的 VACUUM 不能在事务内执行，MySQL 的 OPTIMIZE TABLE 会隐式提交，均不走显式事务。
                    bool useTransaction = databaseType != DatabaseType.Sqlite && databaseType != DatabaseType.MySql;

                    DbTransaction trans = useTransaction ? await con.BeginTransactionAsync() : null;

                    try
                    {
                        await DbRowMapper.ExecuteNonQueryAsync(con, sql, transaction: trans);

                        if (trans != null)
                        {
                            await trans.CommitAsync();
                        }

                        result.IsOK = true;
                    }
                    catch
                    {
                        if (trans != null)
                        {
                            await trans.RollbackAsync();
                        }

                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
            }

            return result;
        }

        private string GetRebuildIndexSql(DatabaseType databaseType, TableIndex index)
        {
            var scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(this.dbInterpreter);

            Script script = databaseType switch
            {
                DatabaseType.SqlServer => (scriptGenerator as SqlServerScriptGenerator).RebuildIndex(index),
                DatabaseType.Postgres or DatabaseType.KingbaseES => (scriptGenerator as PostgresScriptGenerator).RebuildIndex(index),
                DatabaseType.Oracle or DatabaseType.DM => (scriptGenerator as OracleScriptGenerator).RebuildIndex(index),
                DatabaseType.MySql => new Script($"OPTIMIZE TABLE {this.dbInterpreter.GetQuotedDbObjectNameWithSchema(index)}"),
                DatabaseType.Sqlite => new Script("VACUUM"),
                _ => null
            };

            return script?.Content;
        }
    }
}
