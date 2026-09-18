using DatabaseInterpreter.Model;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace DatabaseInterpreter.Core
{
    /// <summary>
    /// DuckDB 解释器：SQL 方言接近 PostgreSQL，故继承 <see cref="PostgresInterpreter"/>。
    /// 能力收窄：仅支持 Table / View / Sequence / Function(macro)；
    /// 无存储过程、触发器、用户自定义类型；不支持二进制批量导入（无 Npgsql COPY）。
    /// 元数据查询使用 information_schema 与 duckdb_* 系统表函数（不依赖 pg_catalog 专有视图）。
    /// </summary>
    public class DuckDbInterpreter : PostgresInterpreter
    {
        public DuckDbInterpreter(ConnectionInfo connectionInfo, DbInterpreterOption option)
            : base(connectionInfo, option)
        {
        }

        public override DatabaseType DatabaseType => DatabaseType.DuckDB;

        // DuckDB.NET 不支持 Npgsql 二进制 COPY，保持参数化 INSERT 回退。
        public override bool SupportBulkCopy => false;

        // 只有表 / 视图 / 序列 / 函数（macro）；无存储过程、触发器、类型、包。
        public override DatabaseObjectType SupportDbObjectType
            => DatabaseObjectType.Table | DatabaseObjectType.View | DatabaseObjectType.Sequence | DatabaseObjectType.Function;

        // DuckDB 默认 schema 为 main（继承自 PostgresInterpreter 的 public 不适用）。
        public override string DefaultSchema => "main";

        public override DbConnector GetDbConnector()
        {
            return new DbConnector(new DuckDbProvider(), new DuckDbConnectionBuilder(), this.ConnectionInfo);
        }

        #region Database
        public override Task<List<Database>> GetDatabasesAsync()
        {
            string sql = @"SELECT database_name AS ""Name"" FROM duckdb_databases() ORDER BY database_name";

            return base.GetDbObjectsAsync<Database>(sql);
        }
        #endregion

        #region Database Schema
        public override Task<List<DatabaseSchema>> GetDatabaseSchemasAsync()
        {
            return base.GetDbObjectsAsync<DatabaseSchema>(this.GetSqlForDuckDbDatabaseSchemas());
        }

        public override Task<List<DatabaseSchema>> GetDatabaseSchemasAsync(DbConnection dbConnection)
        {
            return base.GetDbObjectsAsync<DatabaseSchema>(dbConnection, this.GetSqlForDuckDbDatabaseSchemas());
        }

        private string GetSqlForDuckDbDatabaseSchemas()
        {
            return @"SELECT schema_name AS ""Name"",schema_name AS ""Schema"" FROM information_schema.schemata
                     WHERE schema_name NOT IN ('information_schema','pg_catalog','pg_toast') ORDER BY schema_name";
        }
        #endregion

        #region Table
        public override Task<List<Table>> GetTablesAsync(SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<Table>(this.GetSqlForDuckDbTables(filter, isView: false));
        }

        public override Task<List<Table>> GetTablesAsync(DbConnection dbConnection, SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<Table>(dbConnection, this.GetSqlForDuckDbTables(filter, isView: false));
        }
        #endregion

        #region View
        public override Task<List<View>> GetViewsAsync(SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<View>(this.GetSqlForDuckDbTables(filter, isView: true));
        }

        public override Task<List<View>> GetViewsAsync(DbConnection dbConnection, SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<View>(dbConnection, this.GetSqlForDuckDbTables(filter, isView: true));
        }
        #endregion

        // PostgresInterpreter 的同名方法为 private，这里按 DuckDB 的系统 schema 重写一份。
        private string GetExcludeSystemSchemasCondition(string tableSchema)
        {
            string strSystemSchemas = string.Join(",", this.SystemSchemas.Select(item => $"'{item}'"));
            return $" AND {tableSchema} NOT IN({strSystemSchemas})";
        }

        private string GetSqlForDuckDbTables(SchemaInfoFilter filter, bool isView)
        {
            var sb = this.CreateSqlBuilder();

            sb.Append(@"SELECT t.table_schema AS ""Schema"", t.table_name AS ""Name"" FROM information_schema.tables t");

            sb.Append($" WHERE t.table_type='{(isView ? "VIEW" : "BASE TABLE")}'");
            sb.Append(this.GetExcludeSystemSchemasCondition("t.table_schema"));
            sb.Append(this.GetFilterSchemaCondition(filter, "t.table_schema"));
            sb.Append(this.GetFilterNamesCondition(filter, filter?.TableNames, "t.table_name"));

            sb.Append(" ORDER BY t.table_name");

            return sb.Content;
        }

        #region Table Column
        public override Task<List<TableColumn>> GetTableColumnsAsync(SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<TableColumn>(this.GetSqlForDuckDbTableColumns(filter));
        }

        public override Task<List<TableColumn>> GetTableColumnsAsync(DbConnection dbConnection, SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<TableColumn>(dbConnection, this.GetSqlForDuckDbTableColumns(filter));
        }

        private string GetSqlForDuckDbTableColumns(SchemaInfoFilter filter = null)
        {
            var sb = this.CreateSqlBuilder();

            sb.Append(@"SELECT c.table_schema AS ""Schema"",c.table_name AS ""TableName"",c.column_name AS ""Name"",
                        c.data_type AS ""DataType"",
                        0 AS ""IsUserDefined"",
                        CASE c.is_nullable WHEN 'YES' THEN 1 ELSE 0 END AS ""IsNullable"",
                        COALESCE(c.character_maximum_length,-1) AS ""MaxLength"",
                        c.numeric_precision AS ""Precision"",c.numeric_scale AS ""Scale"",c.ordinal_position AS ""Order"",
                        0 AS ""IsIdentity"",c.table_schema AS ""DateTypeSchema"",
                        c.column_default AS ""DefaultValue""
                        FROM information_schema.columns c");

            sb.Append(" WHERE 1=1");
            sb.Append(this.GetExcludeSystemSchemasCondition("c.table_schema"));
            sb.Append(this.GetFilterSchemaCondition(filter, "c.table_schema"));
            sb.Append(this.GetFilterNamesCondition(filter, filter?.TableNames, "c.table_name"));

            sb.Append(" ORDER BY c.table_name,c.ordinal_position");

            return sb.Content;
        }
        #endregion

        #region Table Primary Key
        public override Task<List<TablePrimaryKeyItem>> GetTablePrimaryKeyItemsAsync(SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<TablePrimaryKeyItem>(this.GetSqlForDuckDbTablePrimaryKeyItems(filter));
        }

        public override Task<List<TablePrimaryKeyItem>> GetTablePrimaryKeyItemsAsync(DbConnection dbConnection, SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<TablePrimaryKeyItem>(dbConnection, this.GetSqlForDuckDbTablePrimaryKeyItems(filter));
        }

        private string GetSqlForDuckDbTablePrimaryKeyItems(SchemaInfoFilter filter = null)
        {
            var sb = this.CreateSqlBuilder();

            sb.Append(@"SELECT tc.table_schema AS ""Schema"",tc.table_name AS ""TableName"",kcu.column_name AS ""ColumnName"",
                        tc.constraint_name AS ""Name"", kcu.ordinal_position AS ""Order"", 0 AS ""IsDesc""
                        FROM information_schema.table_constraints tc
                        JOIN information_schema.key_column_usage kcu
                        ON tc.constraint_catalog=kcu.constraint_catalog AND tc.constraint_schema=kcu.constraint_schema
                        AND tc.constraint_name=kcu.constraint_name AND tc.table_name=kcu.table_name
                        WHERE tc.constraint_type='PRIMARY KEY'");

            sb.Append(this.GetFilterSchemaCondition(filter, "tc.table_schema"));
            sb.Append(this.GetFilterNamesCondition(filter, filter?.TableNames, "tc.table_name"));

            sb.Append(" ORDER BY tc.table_name,kcu.ordinal_position");

            return sb.Content;
        }
        #endregion

        #region Table Index
        public override Task<List<TableIndexItem>> GetTableIndexItemsAsync(SchemaInfoFilter filter = null, bool includePrimaryKey = false)
        {
            return base.GetDbObjectsAsync<TableIndexItem>(this.GetSqlForDuckDbTableIndexItems(filter, includePrimaryKey));
        }

        public override Task<List<TableIndexItem>> GetTableIndexItemsAsync(DbConnection dbConnection, SchemaInfoFilter filter = null, bool includePrimaryKey = false)
        {
            return base.GetDbObjectsAsync<TableIndexItem>(dbConnection, this.GetSqlForDuckDbTableIndexItems(filter, includePrimaryKey));
        }

        private string GetSqlForDuckDbTableIndexItems(SchemaInfoFilter filter = null, bool includePrimaryKey = false)
        {
            var sb = this.CreateSqlBuilder();

            sb.Append($@"SELECT i.schema_name AS ""Schema"", i.table_name AS ""TableName"",
                        COALESCE(i.expressions,'') AS ""ColumnName"", i.index_name AS ""Name"", 'INDEX' AS ""Type"",
                        CASE i.is_unique WHEN true THEN 1 ELSE 0 END AS ""IsUnique"",
                        CASE i.is_primary WHEN true THEN {(includePrimaryKey ? "1" : "0")} ELSE 0 END AS ""IsPrimary"",
                        0 AS ""Clustered""
                        FROM duckdb_indexes() i
                        WHERE {(includePrimaryKey ? "1=1" : "NOT i.is_primary")}");

            sb.Append(this.GetFilterSchemaCondition(filter, "i.schema_name"));
            sb.Append(this.GetFilterNamesCondition(filter, filter?.TableNames, "i.table_name"));

            sb.Append(" ORDER BY i.table_name,i.index_name");

            return sb.Content;
        }
        #endregion

        #region Sequence
        public override Task<List<Sequence>> GetSequencesAsync(SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<Sequence>(this.GetSqlForDuckDbSequences(filter));
        }

        public override Task<List<Sequence>> GetSequencesAsync(DbConnection dbConnection, SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<Sequence>(dbConnection, this.GetSqlForDuckDbSequences(filter));
        }

        private string GetSqlForDuckDbSequences(SchemaInfoFilter filter = null)
        {
            var sb = this.CreateSqlBuilder();

            // duckdb_sequences() 不暴露起始值/步长等属性，按 DuckDB 默认值展示。
            sb.Append(@"SELECT s.schema_name AS ""Schema"", s.sequence_name AS ""Name"", 'BIGINT' AS ""DataType"",
                        1 AS ""StartValue"", 1 AS ""Increment"", 1 AS ""MinValue"", 9223372036854775807 AS ""MaxValue"",
                        0 AS ""Cycled"", 0 AS ""UseCache"", 1 AS ""CacheSize""
                        FROM duckdb_sequences() s
                        WHERE 1=1");

            sb.Append(this.GetFilterSchemaCondition(filter, "s.schema_name"));
            sb.Append(this.GetFilterNamesCondition(filter, filter?.SequenceNames, "s.sequence_name"));

            sb.Append(" ORDER BY s.sequence_name");

            return sb.Content;
        }
        #endregion

        #region Function
        public override Task<List<Function>> GetFunctionsAsync(SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<Function>(this.GetSqlForDuckDbFunctions(filter));
        }

        public override Task<List<Function>> GetFunctionsAsync(DbConnection dbConnection, SchemaInfoFilter filter = null)
        {
            return base.GetDbObjectsAsync<Function>(dbConnection, this.GetSqlForDuckDbFunctions(filter));
        }

        private string GetSqlForDuckDbFunctions(SchemaInfoFilter filter = null)
        {
            var sb = this.CreateSqlBuilder();

            // DuckDB 的"函数"即用户定义的宏（macro / table_macro），内置函数不列出。
            sb.Append(@"SELECT f.schema_name AS ""Schema"", f.function_name AS ""Name"", f.return_type AS ""DataType""
                        FROM duckdb_functions() f
                        WHERE NOT f.internal AND f.function_type IN ('macro','table_macro')");

            sb.Append(this.GetFilterSchemaCondition(filter, "f.schema_name"));
            sb.Append(this.GetFilterNamesCondition(filter, filter?.FunctionNames, "f.function_name"));

            sb.Append(" ORDER BY f.function_name");

            return sb.Content;
        }
        #endregion
    }
}
