using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    /// <summary>
    /// 达梦 DM8 解释器：方言与 Oracle 高度兼容（Schema 即用户、PL/SQL、DUAL/ROWNUM），
    /// 故继承 <see cref="OracleInterpreter"/>，仅替换连接器与批量导入能力。
    /// 注意：官方 NuGet 包 <c>DM.DmProvider</c>（发布者 dameng）驱动，支持 net8.0。
    /// </summary>
    public class DmInterpreter : OracleInterpreter
    {
        public new const int DEFAULT_PORT = DmConnectionBuilder.DefaultPort; // 5236

        public DmInterpreter(ConnectionInfo connectionInfo, DbInterpreterOption option)
            : base(connectionInfo, option)
        {
        }

        public override DatabaseType DatabaseType => DatabaseType.DM;

        // DmBulkCopy 依赖本机 dmfldr_dll.dll 且未验证，保持参数化 INSERT 回退。
        public override bool SupportBulkCopy => false;

        public override DbConnector GetDbConnector()
        {
            return new DbConnector(new DmProvider(), new DmConnectionBuilder(), ConnectionInfo);
        }
    }
}
