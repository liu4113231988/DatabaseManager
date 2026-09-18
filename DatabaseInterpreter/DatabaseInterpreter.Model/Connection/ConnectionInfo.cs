using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Model
{
    public class ConnectionInfo : DatabaseAccountInfo
    {
        public string Database { get; set; }
        public bool NeedCheckServerVersion { get; set; }
        /// <summary>是否以只读模式连接（当前仅 DuckDB 使用：ACCESS_MODE=read_only）。</summary>
        public bool UseReadOnly { get; set; }
    }
}
