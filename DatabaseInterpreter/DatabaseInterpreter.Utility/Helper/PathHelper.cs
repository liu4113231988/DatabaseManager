using System;

namespace DatabaseInterpreter.Utility
{
    public class PathHelper
    {
        public static string GetAssemblyFolder()
        {
            return AppContext.BaseDirectory;
        }
    }
}
