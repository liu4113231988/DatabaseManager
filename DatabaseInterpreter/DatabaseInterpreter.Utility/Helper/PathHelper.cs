using System.IO;

namespace DatabaseInterpreter.Utility
{
    public class PathHelper
    {
        public static string GetAssemblyFolder()
        {
            return Path.GetDirectoryName(typeof(PathHelper).Assembly.Location);
        }
    }
}
