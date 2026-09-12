namespace DatabaseInterpreter.Model
{
    /// <summary>当前驱动和连接构建器支持的集成认证入口。</summary>
    public static class DatabaseAuthentication
    {
        public static bool SupportsIntegratedSecurity(DatabaseType type)
            => type == DatabaseType.SqlServer || type == DatabaseType.Oracle || type == DatabaseType.Postgres;

        public static bool AllowsIntegratedUserName(DatabaseType type)
            => type == DatabaseType.Postgres;
    }
}
