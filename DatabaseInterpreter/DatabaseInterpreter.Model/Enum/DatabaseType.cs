namespace DatabaseInterpreter.Model
{
    public enum DatabaseType
    {
        Unknown = 0,
        SqlServer = 1,
        MySql = 2,
        Oracle = 3,
        Postgres = 4,
        Sqlite = 5,
        /// <summary>人大金仓 KingbaseES（首期按 PG 兼容模式接入）。</summary>
        KingbaseES = 6,
        /// <summary>DuckDB（嵌入式分析型数据库，文件/内存连接模型）。</summary>
        DuckDB = 7,
        /// <summary>达梦 DM8（Oracle 兼容方言；官方 NuGet 包 DM.DmProvider）。</summary>
        DM = 8
    }
}
