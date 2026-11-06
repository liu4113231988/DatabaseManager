using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Model
{
    public class ConnectionInfo : DatabaseAccountInfo
    {
        public string Database { get; set; }
        public bool NeedCheckServerVersion { get; set; }
        /// <summary>是否以只读模式连接（当前仅 DuckDB 使用：ACCESS_MODE=read_only）。</summary>
        public bool UseReadOnly { get; set; }
        /// <summary>SQLite 普通连接只打开已有文件；内部配置库可保留自动创建行为。</summary>
        public bool RequireExistingDatabase { get; set; }
    }
}
