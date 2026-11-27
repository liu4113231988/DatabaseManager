using DatabaseInterpreter.Model;

namespace DatabaseManager.Core
{
    /// <summary>
    /// 达梦 DM 诊断：Oracle 兼容方言，复用基类的解释器无关 SQL 路径。
    /// </summary>
    public class DmDiagnosis : DbDiagnosis
    {
        public override DatabaseType DatabaseType => DatabaseType.DM;
        public DmDiagnosis(ConnectionInfo connectionInfo) : base(connectionInfo) { }
    }
}
