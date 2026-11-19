using System;
using System.IO;

namespace DatabaseManager.FileUtility
{
    public class BaseWriter
    {
        public readonly string AssemblyFolder = AppContext.BaseDirectory;
        public readonly string DefaultSaveFolder = "export";
        public readonly string TemporaryFolder = "temp";

        protected void CheckFolder(string folder)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }       
    }
}
