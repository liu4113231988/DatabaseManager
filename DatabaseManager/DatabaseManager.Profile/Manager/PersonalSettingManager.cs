using Dapper;
using DatabaseManager.Profile.Model;
using DatabaseManager.Profile.Security;
using System.Linq;
using System.Threading.Tasks;

namespace DatabaseManager.Profile.Manager
{
    public class PersonalSettingManager : ProfileBaseManager
    {
        public static async Task<PersonalSetting> GetPersonalSetting()
        {
            if (ExistsProfileDataFile())
            {
                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    string sql = "SELECT Id, LockPassword FROM PersonalSetting WHERE Id=1";

                    PersonalSetting setting = (await connection.QueryAsync<PersonalSetting>(sql))?.FirstOrDefault();

                    if(setting!=null && !string.IsNullOrEmpty(setting.LockPassword))
                    {
                        setting.LockPassword = await UnprotectSecretAsync(connection, "PersonalSetting", "LockPassword", setting.Id, setting.LockPassword);
                    }

                    return setting;
                }
            }

            return null;
        }

        public static async Task<bool> Save(PersonalSetting setting)
        {
            if (ExistsProfileDataFile())
            {
                using (var connection = CreateDbConnection())
                {
                    await connection.OpenAsync();

                    var trans = await connection.BeginTransactionAsync();

                    string sql = "UPDATE PersonalSetting SET LockPassword=@LockPassword WHERE Id=1";

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = sql;

                    string lockPassword = string.IsNullOrEmpty(setting.LockPassword) ? null : CredentialProtector.Default.Protect(setting.LockPassword);

                    cmd.Parameters.AddWithValue("@LockPassword", GetParameterValue(lockPassword));

                    int result = await cmd.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        await trans.CommitAsync();

                        return true;
                    }
                }
            }

            return false;
        }
    }
}
