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
        KingbaseES = 6
    }
}
