using System;

namespace DatabaseManager.Model
{
    public class ConnectionDisplayInfo
    {
        public bool Checked { get; set; }
        public string Id { get; set; }
        public string Server { get; set; }
        public string Port { get; set; }
        public bool IntegratedSecurity { get; set; }
        public string UserName { get; set; }
        public string Name { get; set; }
        public string Database { get; set; }
        public int Priority { get; set; }

        public object Profile { get; set; }
    }
}
