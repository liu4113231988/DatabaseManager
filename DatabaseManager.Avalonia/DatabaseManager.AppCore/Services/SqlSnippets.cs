namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 内置 SQL 代码片段（只读，随应用提供；在脚本库窗口的「内置片段」页展示并可插入编辑器）。
/// </summary>
public static class SqlSnippets
{
    public static IReadOnlyList<ScriptLibraryItem> BuiltIn { get; } = new[]
    {
        new ScriptLibraryItem
        {
            Id = "snippet.select-all",
            Name = "查询全表",
            Category = "内置片段",
            SqlText = "SELECT *\nFROM table_name\nWHERE condition\nORDER BY column;\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.select-columns",
            Name = "指定列查询",
            Category = "内置片段",
            SqlText = "SELECT t.col1,\n       t.col2,\n       COUNT(*) AS cnt\nFROM table_name t\nWHERE t.col1 IS NOT NULL\nGROUP BY t.col1, t.col2\nHAVING COUNT(*) > 1\nORDER BY cnt DESC;\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.join",
            Name = "表连接（JOIN）",
            Category = "内置片段",
            SqlText = "SELECT a.id,\n       a.name,\n       b.detail\nFROM table_a a\nINNER JOIN table_b b ON b.a_id = a.id\nWHERE a.status = 1;\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.insert",
            Name = "插入数据",
            Category = "内置片段",
            SqlText = "INSERT INTO table_name (col1, col2, col3)\nVALUES (value1, value2, value3);\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.update",
            Name = "更新数据",
            Category = "内置片段",
            SqlText = "UPDATE table_name\nSET col1 = value1,\n    col2 = value2\nWHERE id = 1;\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.delete",
            Name = "删除数据",
            Category = "内置片段",
            SqlText = "-- 建议先用 SELECT 确认影响范围\nSELECT COUNT(*) FROM table_name WHERE condition;\n\nDELETE FROM table_name\nWHERE condition;\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.paging-mysql",
            Name = "分页（MySQL / PostgreSQL / SQLite）",
            Category = "内置片段",
            SqlText = "SELECT *\nFROM table_name\nORDER BY id\nLIMIT 20 OFFSET 40;  -- 第 3 页，每页 20 行\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.paging-sqlserver",
            Name = "分页（SQL Server / Oracle 12c+）",
            Category = "内置片段",
            SqlText = "SELECT *\nFROM table_name\nORDER BY id\nOFFSET 40 ROWS FETCH NEXT 20 ROWS ONLY;  -- 第 3 页，每页 20 行\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.transaction",
            Name = "事务模板",
            Category = "内置片段",
            SqlText = "BEGIN TRANSACTION;\n\nUPDATE accounts SET balance = balance - 100 WHERE id = 1;\nUPDATE accounts SET balance = balance + 100 WHERE id = 2;\n\n-- 确认无误后提交\nCOMMIT;\n-- 出错时回滚\n-- ROLLBACK;\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.create-table",
            Name = "建表模板",
            Category = "内置片段",
            SqlText = "CREATE TABLE table_name (\n    id         BIGINT       NOT NULL PRIMARY KEY,\n    name       VARCHAR(100) NOT NULL,\n    status     INT          NULL DEFAULT 0,\n    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP\n);\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.create-index",
            Name = "创建索引",
            Category = "内置片段",
            SqlText = "CREATE INDEX idx_table_name_col1 ON table_name (col1);\n\n-- 联合索引\nCREATE INDEX idx_table_name_col1_col2 ON table_name (col1, col2);\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.upsert",
            Name = "存在则更新，否则插入",
            Category = "内置片段",
            SqlText = "-- MySQL\nINSERT INTO table_name (id, col1)\nVALUES (1, 'value')\nON DUPLICATE KEY UPDATE col1 = VALUES(col1);\n\n-- PostgreSQL / SQLite\n-- INSERT INTO table_name (id, col1) VALUES (1, 'value')\n-- ON CONFLICT (id) DO UPDATE SET col1 = EXCLUDED.col1;\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.find-duplicates",
            Name = "查找重复行",
            Category = "内置片段",
            SqlText = "SELECT col1, COUNT(*) AS cnt\nFROM table_name\nGROUP BY col1\nHAVING COUNT(*) > 1;\n",
        },
        new ScriptLibraryItem
        {
            Id = "snippet.explain",
            Name = "执行计划",
            Category = "内置片段",
            SqlText = "-- MySQL / PostgreSQL\nEXPLAIN SELECT * FROM table_name WHERE col1 = 1;\n\n-- 实际执行并输出耗时（PostgreSQL / MySQL 8.0.18+）\nEXPLAIN ANALYZE SELECT * FROM table_name WHERE col1 = 1;\n\n-- SQLite\nEXPLAIN QUERY PLAN SELECT * FROM table_name WHERE col1 = 1;\n",
        },
    };
}
