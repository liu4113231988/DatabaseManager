using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    /// <summary>
    /// KingbaseES 解释器。首期复用已验证的 PostgreSQL SQL 语义，
    /// 但使用 Kdbndp 驱动并以独立 DatabaseType 暴露，后续可按兼容模式覆盖 catalog SQL。
    /// </summary>
    public class KingbaseInterpreter : PostgresInterpreter
    {
        public new const int DEFAULT_PORT = KingbaseConnectionBuilder.DefaultPort;

        public KingbaseInterpreter(ConnectionInfo connectionInfo, DbInterpreterOption option)
            : base(connectionInfo, option)
        {
        }

        public override DatabaseType DatabaseType => DatabaseType.KingbaseES;

        public override DbConnector GetDbConnector()
            => new DbConnector(new KingbaseProvider(), new KingbaseConnectionBuilder(), ConnectionInfo);
    }
}
