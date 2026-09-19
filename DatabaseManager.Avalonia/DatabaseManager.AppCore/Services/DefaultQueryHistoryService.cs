using System.IO;
using Microsoft.Data.Sqlite;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 查询历史服务实现：SQLite 存储（Profiles/query-history.db3），上限 500 条，收藏项不参与裁剪。
/// 不迁移旧 JSON 文件（query-history.json 直接忽略）。
/// </summary>
public class DefaultQueryHistoryService : IQueryHistoryService
{
    private const int MaxEntries = 500;
    private const int MaxSqlLength = 64 * 1024;

    private static readonly object FileLock = new();

    private readonly string _connectionString;

    public DefaultQueryHistoryService()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Profiles");
        Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dir, "query-history.db3"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();

        EnsureTable();
    }

    public void Add(QueryHistoryEntry entry)
    {
        if (entry is null)
        {
            return;
        }

        if (entry.SqlText is { Length: > MaxSqlLength })
        {
            entry.SqlText = entry.SqlText[..MaxSqlLength];
        }

        lock (FileLock)
        {
            try
            {
                using var connection = OpenConnection();
                using var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO QueryHistory (Time, ConnectionName, Database, SqlText, IsSuccess, RowCount, ElapsedMilliseconds, ErrorMessage, IsFavorite)
                    VALUES (@time, @connectionName, @database, @sqlText, @isSuccess, @rowCount, @elapsed, @errorMessage, @isFavorite);
                    SELECT last_insert_rowid();
                    """;
                insert.Parameters.AddWithValue("@time", entry.Time);
                insert.Parameters.AddWithValue("@connectionName", entry.ConnectionName ?? string.Empty);
                insert.Parameters.AddWithValue("@database", entry.Database ?? string.Empty);
                insert.Parameters.AddWithValue("@sqlText", entry.SqlText ?? string.Empty);
                insert.Parameters.AddWithValue("@isSuccess", entry.IsSuccess ? 1 : 0);
                insert.Parameters.AddWithValue("@rowCount", entry.RowCount);
                insert.Parameters.AddWithValue("@elapsed", entry.ElapsedMilliseconds);
                insert.Parameters.AddWithValue("@errorMessage", (object?)entry.ErrorMessage ?? DBNull.Value);
                insert.Parameters.AddWithValue("@isFavorite", entry.IsFavorite ? 1 : 0);

                entry.Id = Convert.ToInt64(insert.ExecuteScalar());
                TrimExcessEntries(connection);
            }
            catch
            {
                // 写入失败（磁盘只读等）时忽略，不影响查询主流程。
            }
        }
    }

    public void Update(QueryHistoryEntry entry)
    {
        if (entry is null || entry.Id <= 0)
        {
            return;
        }

        lock (FileLock)
        {
            try
            {
                using var connection = OpenConnection();
                using var update = connection.CreateCommand();
                update.CommandText = """
                    UPDATE QueryHistory
                    SET Time = @time, ConnectionName = @connectionName, Database = @database, SqlText = @sqlText,
                        IsSuccess = @isSuccess, RowCount = @rowCount, ElapsedMilliseconds = @elapsed,
                        ErrorMessage = @errorMessage, IsFavorite = @isFavorite
                    WHERE Id = @id
                    """;
                update.Parameters.AddWithValue("@time", entry.Time);
                update.Parameters.AddWithValue("@connectionName", entry.ConnectionName ?? string.Empty);
                update.Parameters.AddWithValue("@database", entry.Database ?? string.Empty);
                update.Parameters.AddWithValue("@sqlText", entry.SqlText ?? string.Empty);
                update.Parameters.AddWithValue("@isSuccess", entry.IsSuccess ? 1 : 0);
                update.Parameters.AddWithValue("@rowCount", entry.RowCount);
                update.Parameters.AddWithValue("@elapsed", entry.ElapsedMilliseconds);
                update.Parameters.AddWithValue("@errorMessage", (object?)entry.ErrorMessage ?? DBNull.Value);
                update.Parameters.AddWithValue("@isFavorite", entry.IsFavorite ? 1 : 0);
                update.Parameters.AddWithValue("@id", entry.Id);
                update.ExecuteNonQuery();
            }
            catch
            {
                // 更新失败时忽略（收藏标记丢失可重新切换）。
            }
        }
    }

    public IReadOnlyList<QueryHistoryEntry> GetRecent(int maxCount = 200)
    {
        var result = new List<QueryHistoryEntry>();
        lock (FileLock)
        {
            try
            {
                using var connection = OpenConnection();
                using var select = connection.CreateCommand();
                select.CommandText = """
                    SELECT Id, Time, ConnectionName, Database, SqlText, IsSuccess, RowCount, ElapsedMilliseconds, ErrorMessage, IsFavorite
                    FROM QueryHistory
                    ORDER BY Id DESC
                    LIMIT @limit
                    """;
                select.Parameters.AddWithValue("@limit", Math.Max(1, maxCount));

                using var reader = select.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new QueryHistoryEntry
                    {
                        Id = reader.GetInt64(0),
                        Time = reader.GetDateTime(1),
                        ConnectionName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Database = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        SqlText = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        IsSuccess = !reader.IsDBNull(5) && reader.GetInt64(5) != 0,
                        RowCount = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                        ElapsedMilliseconds = reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                        ErrorMessage = reader.IsDBNull(8) ? null : reader.GetString(8),
                        IsFavorite = !reader.IsDBNull(9) && reader.GetInt64(9) != 0,
                    });
                }
            }
            catch
            {
                // 读取失败时返回空列表，不影响主流程。
            }
        }

        return result;
    }

    public void Clear()
    {
        lock (FileLock)
        {
            try
            {
                using var connection = OpenConnection();
                using var delete = connection.CreateCommand();
                delete.CommandText = "DELETE FROM QueryHistory";
                delete.ExecuteNonQuery();
            }
            catch
            {
                // 清空失败时忽略。
            }
        }
    }

    /// <summary>建库建表（幂等）。</summary>
    private void EnsureTable()
    {
        try
        {
            using var connection = OpenConnection();
            using var create = connection.CreateCommand();
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS QueryHistory (
                    Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                    Time               TEXT    NOT NULL,
                    ConnectionName     TEXT    NOT NULL DEFAULT '',
                    Database           TEXT    NOT NULL DEFAULT '',
                    SqlText            TEXT    NOT NULL DEFAULT '',
                    IsSuccess          INTEGER NOT NULL DEFAULT 0,
                    RowCount           INTEGER NOT NULL DEFAULT 0,
                    ElapsedMilliseconds INTEGER NOT NULL DEFAULT 0,
                    ErrorMessage       TEXT,
                    IsFavorite         INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS IX_QueryHistory_Id_Favorite ON QueryHistory (Id, IsFavorite);
                """;
            create.ExecuteNonQuery();
        }
        catch
        {
            // 建表失败时忽略：后续操作会再次尝试。
        }
    }

    /// <summary>裁剪超出上限的记录：只删除最旧的非收藏项，收藏项不参与裁剪。</summary>
    private void TrimExcessEntries(SqliteConnection connection)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM QueryHistory";
        var count = Convert.ToInt64(countCommand.ExecuteScalar() ?? 0L);
        var excess = count - MaxEntries;
        if (excess <= 0)
        {
            return;
        }

        // 定位最旧 excess 条非收藏记录的最大 Id 作为删除上界。
        using var cutoffCommand = connection.CreateCommand();
        cutoffCommand.CommandText = """
            SELECT COALESCE(MAX(Id), 0) FROM (
                SELECT Id FROM QueryHistory WHERE IsFavorite = 0 ORDER BY Id ASC LIMIT @excess
            )
            """;
        cutoffCommand.Parameters.AddWithValue("@excess", excess);
        var cutoff = Convert.ToInt64(cutoffCommand.ExecuteScalar() ?? 0L);
        if (cutoff <= 0)
        {
            return;
        }

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM QueryHistory WHERE IsFavorite = 0 AND Id <= @cutoff";
        delete.Parameters.AddWithValue("@cutoff", cutoff);
        delete.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
