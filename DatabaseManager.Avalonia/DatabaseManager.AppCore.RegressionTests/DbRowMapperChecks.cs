using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;
using Microsoft.Data.Sqlite;

internal static class DbRowMapperChecks
{
    public static async Task RunAsync()
    {
        await VerifyMappingAsync();
        Console.WriteLine("Row mapper checks passed: name matching, nulls, conversions, parameters and scalar reads.");
    }

    private static async Task VerifyMappingAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbm-row-mapper-{Guid.NewGuid():N}.db");

        try
        {
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = path,
                Pooling = false
            }.ConnectionString))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = """
                        CREATE TABLE mapped (
                            "Name" TEXT, "Schema" TEXT, "Order" INTEGER, "DataType" TEXT,
                            "IsNullable" INTEGER, "MaxLength" INTEGER, "Precision" INTEGER,
                            "DefaultValue" TEXT, "UnmappedColumn" TEXT);
                        INSERT INTO mapped VALUES ('child', 'dbo', 2, 'TEXT', 1, 64, NULL, NULL, 'ignored');
                        INSERT INTO mapped VALUES ('parent', 'main', 1, 'INTEGER', 0, NULL, 10, '0', 'ignored');
                        """;
                    await command.ExecuteNonQueryAsync();
                }

                // 列名先精确后忽略大小写匹配，未匹配的列与只读/复杂类型属性都不参与映射。
                var columns = await DbRowMapper.ReadObjectsAsync<TableColumn>(connection,
                    """
                    SELECT name AS "Name", schema AS "Schema", "Order", datatype AS "datatype",
                           IsNullable, MaxLength, Precision, DefaultValue, UnmappedColumn
                    FROM mapped ORDER BY "Order"
                    """);

                Check(columns.Count == 2, "映射器必须读取全部结果行。");
                Check(columns[0].DataType == "INTEGER" && columns[1].DataType == "TEXT", "列名必须支持忽略大小写的属性匹配。");
                Check(columns[0].Name == "parent" && columns[1].Name == "child", "文本列必须映射到 string 属性并保持结果顺序。");
                Check(columns[0].Order == 1 && columns[1].Order == 2, "整数列必须映射到 int 属性。");
                Check(!columns[0].IsNullable && columns[1].IsNullable, "整数 1/0 必须映射为 bool，Dapper 在此会抛出转换异常。");
                Check(columns[1].MaxLength == 64 && columns[0].Precision == 10, "整数列必须映射到 long? 属性。");
                Check(columns[0].MaxLength == null && columns[1].Precision == null, "DBNull 必须映射为可空数值属性的 null。");
                Check(columns[0].DefaultValue == "0" && columns[1].DefaultValue == null, "文本列必须映射到 string 属性。");
                Check(!columns[0].IsIdentity && !columns[1].IsIdentity, "无对应结果列的属性必须保持默认值。");
                Check(columns[0].IsRequired && !columns[1].IsRequired, "只读计算属性必须由依赖属性推导而非结果列写入。");

                // 数值来源可以映射到 string 属性（Oracle / MySQL 的百分比统计列）。
                var fragments = await DbRowMapper.ReadObjectsAsync<IndexFragmentation>(connection,
                    """SELECT 12.5 AS "FragmentationPercent", 'dbo' AS "Schema", 'mapped' AS "TableName", 'ix' AS "IndexName" FROM mapped WHERE "Name"='child'""");

                Check(fragments.Single().FragmentationPercent == "12.5", "数值列必须可映射到 string 属性。");

                // 非空值类型遇到 DBNull 保持属性初始值，复杂类型忽略同名列。
                var primaryKey = (await DbRowMapper.ReadObjectsAsync<TablePrimaryKey>(connection,
                    """SELECT 'pk' AS "Name", NULL AS "Clustered", '[]' AS "Columns" FROM mapped WHERE "Name"='child'""")).Single();

                Check(primaryKey.Clustered, "DBNull 不能让带初始值的非空值类型属性抛异常。");
                Check(primaryKey.Columns.Count == 0, "复杂类型属性必须忽略同名结果列。");

                // 参数化读取与标量读取。
                var parameterNames = new List<(string Name, object Value)>() { ("@Schema", "dbo") };

                var filtered = await DbRowMapper.ReadObjectsAsync<TableColumn>(connection,
                    """SELECT "Name", "Schema" FROM mapped WHERE "Schema"=@Schema""", parameters: parameterNames);

                Check(filtered.Single().Name == "child", "参数化查询必须过滤结果行。");

                int? count = await DbRowMapper.ReadFirstValueAsync<int?>(connection,
                    """SELECT COUNT(1) FROM mapped WHERE "Name"=@Name""",
                    parameters: new List<(string Name, object Value)>() { ("@Name", "child") });

                Check(count == 1, "标量首值读取必须返回聚合结果。");

                Check(await DbRowMapper.ReadFirstValueAsync<int?>(connection,
                    """SELECT COUNT(1) FROM mapped WHERE "Name"=@Name""",
                    parameters: new List<(string Name, object Value)>() { ("@Name", "missing") }) == 0,
                    "无匹配行时聚合标量必须读取为 0。");

                var names = await DbRowMapper.ReadColumnValuesAsync<string>(connection, """SELECT "Name" FROM mapped ORDER BY "Order" DESC""");

                Check(names.SequenceEqual(new[] { "child", "parent" }), "首列值列表必须按结果顺序返回。");

                Check(await DbRowMapper.ReadFirstValueAsync<string>(connection, """SELECT "DefaultValue" FROM mapped WHERE "Name"='child'""") == null,
                    "DBNull 的首列值必须读取为 null。");

                Check(await DbRowMapper.ExecuteScalarAsync<long?>(connection, """SELECT MAX("Order") FROM mapped WHERE "Name"='missing'""") == null,
                    "无结果行的标量查询必须返回默认值而非抛异常。");

                Check(await DbRowMapper.ReadFirstValueAsync<int>(connection, "") == 0, "空 SQL 必须返回空结果而不执行命令。");

                Check((await DbRowMapper.ReadObjectsAsync<TableColumn>(connection, "")).Count == 0, "空 SQL 的对象读取必须返回空列表。");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
