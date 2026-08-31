namespace DatabaseInterpreter.Core
{
    /// <summary>人大金仓官方 Kdbndp ADO.NET 提供程序标识。</summary>
    public class KingbaseProvider : IDbProvider
    {
        public string ProviderName => "Kdbndp";
    }
}
