namespace DatabaseInterpreter.Core
{
    /// <summary>达梦 ADO.NET 提供商标识（官方 NuGet 包 DM.DmProvider，发布者 dameng）。</summary>
    public class DmProvider : IDbProvider
    {
        public string ProviderName => "DmProvider";
    }
}
