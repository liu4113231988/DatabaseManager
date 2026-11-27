using DatabaseInterpreter.Model;

namespace DatabaseManager.Core
{
    /// <summary>
    /// 金仓 KingbaseES 诊断：PG 兼容模式，复用基类的解释器无关 SQL 路径。
    /// </summary>
    public class KingbaseDiagnosis : DbDiagnosis
    {
        public override DatabaseType DatabaseType => DatabaseType.KingbaseES;
        public KingbaseDiagnosis(ConnectionInfo connectionInfo) : base(connectionInfo) { }
    }
}
