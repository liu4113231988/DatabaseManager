using DatabaseInterpreter.Model;

namespace DatabaseManager.Core
{
    /// <summary>
    /// DuckDB 诊断：SQL 方言接近 PostgreSQL，复用基类的解释器无关 SQL 路径。
    /// </summary>
    public class DuckDbDiagnosis : DbDiagnosis
    {
        public override DatabaseType DatabaseType => DatabaseType.DuckDB;
        public DuckDbDiagnosis(ConnectionInfo connectionInfo) : base(connectionInfo) { }
    }
}
