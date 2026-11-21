using DatabaseInterpreter.Model;
using System;

namespace DatabaseInterpreter.Core
{
    /// <summary>
    /// KingbaseES 解释器。首期复用已验证的 PostgreSQL SQL 语义，
    /// 但使用 Kdbndp 驱动并以独立 DatabaseType 暴露，后续可按兼容模式覆盖 catalog SQL。
    /// </summary>
    public class KingbaseInterpreter : PostgresInterpreter
    {
#if DATABASE_MANAGER_AOT
        public new const int DEFAULT_PORT = 54321;
#else
        public new const int DEFAULT_PORT = KingbaseConnectionBuilder.DefaultPort;
#endif

        public KingbaseInterpreter(ConnectionInfo connectionInfo, DbInterpreterOption option)
            : base(connectionInfo, option)
        {
        }

        public override DatabaseType DatabaseType => DatabaseType.KingbaseES;

        /// <summary>
        /// PostgreSQL 的二进制 COPY 实现依赖 NpgsqlConnection，不能用于 Kdbndp。
        /// 在拿到并验证供应商批量接口前，保持参数化 INSERT 回退路径。
        /// </summary>
        public override bool SupportBulkCopy => false;

        public override DbConnector GetDbConnector()
        {
#if DATABASE_MANAGER_AOT
            throw new NotSupportedException("KingbaseES/Kdbndp is not supported in the Native AOT preview.");
#else
            return new DbConnector(new KingbaseProvider(), new KingbaseConnectionBuilder(), ConnectionInfo);
#endif
        }
    }
}
