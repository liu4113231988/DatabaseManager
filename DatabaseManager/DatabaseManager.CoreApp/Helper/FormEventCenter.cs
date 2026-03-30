using DatabaseInterpreter.Model;

namespace DatabaseManager.Helper
{
    public delegate void RefreshNavigatorFolderHandler();
    public delegate void DatabaseDisconnectedHandler(ConnectionInfo connectionInfo);

    public class FormEventCenter
    {       
        public static RefreshNavigatorFolderHandler OnRefreshNavigatorFolder;
        public static DatabaseDisconnectedHandler OnDatabaseDisconnected;
    }
}
