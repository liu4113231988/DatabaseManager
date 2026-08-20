using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 基于 <see cref="DbInterpreter"/> / <see cref="DbScriptGenerator"/> 的表设计服务实现。
/// 加载表结构、生成 CREATE/ALTER 脚本、在事务内执行保存。
/// </summary>
public class DefaultTableDesignService : ITableDesignService
{
    private readonly IDbConnectionService _connectionService;

    public DefaultTableDesignService(IDbConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public async Task<TableDesignLoadResult> LoadTableAsync(
        string connectionName,
        string databaseName,
        string tableName,
        string? schema,
        bool isNew,
        CancellationToken cancellationToken = default)
    {
        var connection = FindConnection(connectionName);
        if (connection is null)
        {
            return new TableDesignLoadResult { ErrorMessage = $"未找到连接 '{connectionName}'。" };
        }

        var interpreter = CreateInterpreter(connection, databaseName);
        var design = new TableDesignInfo
        {
            DatabaseName = databaseName,
            Schema = schema,
            Name = tableName,
            IsNew = isNew,
        };

        if (isNew)
        {
            // 新建表：返回空壳，UI 填充列定义。
            return new TableDesignLoadResult { IsSuccess = true, Design = design };
        }

        try
        {
            var filter = new SchemaInfoFilter
            {
                Schema = schema,
                TableNames = new[] { tableName },
                DatabaseObjectType = DatabaseObjectType.Table
                    | DatabaseObjectType.Column
                    | DatabaseObjectType.PrimaryKey
                    | DatabaseObjectType.Index
                    | DatabaseObjectType.ForeignKey
                    | DatabaseObjectType.Constraint,
            };

            var schemaInfo = await interpreter.GetSchemaInfoAsync(filter);

            var table = schemaInfo.Tables.FirstOrDefault();
            design.Comment = table?.Comment ?? string.Empty;

            // 列
            design.Columns = schemaInfo.TableColumns
                .OrderBy(c => c.Order)
                .Select(c => new TableDesignColumn
                {
                    Name = c.Name,
                    DataType = c.DataType ?? string.Empty,
                    MaxLength = c.MaxLength,
                    Precision = c.Precision,
                    Scale = c.Scale,
                    IsNullable = c.IsNullable,
                    IsIdentity = c.IsIdentity,
                    DefaultValue = c.DefaultValue ?? string.Empty,
                    ComputeExp = c.ComputeExp ?? string.Empty,
                    Comment = c.Comment ?? string.Empty,
                    Order = c.Order,
                })
                .ToList();

            // 主键
            var pk = schemaInfo.TablePrimaryKeys.FirstOrDefault();
            if (pk is not null)
            {
                design.PrimaryKey = new TableDesignKey
                {
                    Name = pk.Name,
                    Clustered = pk.Clustered,
                    Columns = pk.Columns.Select(c => c.ColumnName).ToList(),
                };
            }

            // 索引
            design.Indexes = schemaInfo.TableIndexes
                .Where(i => !i.IsPrimary)
                .Select(i => new TableDesignIndex
                {
                    Name = i.Name,
                    IsUnique = i.IsUnique,
                    Columns = i.Columns.Select(c => c.ColumnName).ToList(),
                })
                .ToList();

            // 外键
            design.ForeignKeys = schemaInfo.TableForeignKeys
                .Select(fk => new TableDesignForeignKey
                {
                    Name = fk.Name,
                    ReferencedSchema = fk.ReferencedSchema,
                    ReferencedTableName = fk.ReferencedTableName,
                    UpdateCascade = fk.UpdateCascade,
                    DeleteCascade = fk.DeleteCascade,
                    Columns = fk.Columns.Select(c => new ForeignKeyMapping
                    {
                        ColumnName = c.ColumnName,
                        ReferencedColumnName = c.ReferencedColumnName,
                    }).ToList(),
                })
                .ToList();

            // 约束
            design.Constraints = schemaInfo.TableConstraints
                .Select(c => new TableDesignConstraint
                {
                    Name = c.Name,
                    Definition = c.Definition,
                })
                .ToList();

            return new TableDesignLoadResult { IsSuccess = true, Design = design };
        }
        catch (Exception ex)
        {
            return new TableDesignLoadResult { ErrorMessage = ex.Message };
        }
    }

    public async Task<TableDesignScriptResult> GenerateScriptsAsync(
        string connectionName,
        string databaseName,
        TableDesignInfo design,
        CancellationToken cancellationToken = default)
    {
        var connection = FindConnection(connectionName);
        if (connection is null)
        {
            return new TableDesignScriptResult { ErrorMessage = $"未找到连接 '{connectionName}'。" };
        }

        if (string.IsNullOrWhiteSpace(design.Name))
        {
            return new TableDesignScriptResult { ErrorMessage = "表名不能为空。" };
        }

        try
        {
            var interpreter = CreateInterpreter(connection, databaseName);
            var scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(interpreter);

            List<string> scriptContents;

            if (design.IsNew)
            {
                scriptContents = BuildCreateScripts(scriptGenerator, design);
            }
            else
            {
                // 加载数据库当前结构用于 diff。
                var current = await LoadTableAsync(
                    connectionName,
                    databaseName,
                    design.Name,
                    design.Schema,
                    isNew: false,
                    cancellationToken);

                if (!current.IsSuccess)
                {
                    return new TableDesignScriptResult { ErrorMessage = current.ErrorMessage };
                }

                scriptContents = BuildAlterScripts(interpreter, scriptGenerator, current.Design, design);
            }

            if (scriptContents.Count == 0)
            {
                return new TableDesignScriptResult { IsSuccess = true, HasScripts = false };
            }

            string delimiter = interpreter.ScriptsDelimiter;
            string script = string.Join(Environment.NewLine + delimiter + Environment.NewLine, scriptContents);

            return new TableDesignScriptResult
            {
                IsSuccess = true,
                HasScripts = true,
                Script = script,
            };
        }
        catch (Exception ex)
        {
            return new TableDesignScriptResult { ErrorMessage = ex.Message };
        }
    }

    public async Task<TableDesignSaveResult> SaveAsync(
        string connectionName,
        string databaseName,
        TableDesignInfo design,
        CancellationToken cancellationToken = default)
    {
        var result = await GenerateScriptsAsync(connectionName, databaseName, design, cancellationToken);

        if (!result.IsSuccess)
        {
            return new TableDesignSaveResult { IsSuccess = false, ErrorMessage = result.ErrorMessage };
        }

        if (!result.HasScripts)
        {
            return new TableDesignSaveResult { IsSuccess = true, ScriptCount = 0 };
        }

        var connection = FindConnection(connectionName);
        if (connection is null)
        {
            return new TableDesignSaveResult { IsSuccess = false, ErrorMessage = $"未找到连接 '{connectionName}'。" };
        }

        var interpreter = CreateInterpreter(connection, databaseName);

        try
        {
            using var dbConnection = interpreter.CreateConnection();
            if (dbConnection.State != ConnectionState.Open)
            {
                await dbConnection.OpenAsync(cancellationToken);
            }

            var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);
            int count = 0;

            try
            {
                // 按分隔符拆分为多条脚本，在单事务内顺序执行。
                var statements = SplitStatements(result.Script, interpreter.ScriptsDelimiter);

                foreach (var sql in statements)
                {
                    if (string.IsNullOrWhiteSpace(sql))
                        continue;

                    var commandInfo = new CommandInfo
                    {
                        CommandText = sql.Trim().TrimEnd(';'),
                        Transaction = transaction,
                        CancellationToken = cancellationToken,
                    };

                    var exec = await interpreter.ExecuteNonQueryAsync(dbConnection, commandInfo);
                    if (exec is not null && exec.HasError)
                    {
                        throw new Exception(exec.Message);
                    }

                    count++;
                }

                await transaction.CommitAsync(cancellationToken);

                return new TableDesignSaveResult { IsSuccess = true, ScriptCount = count };
            }
            catch
            {
                try { await transaction.RollbackAsync(cancellationToken); } catch { /* 忽略 */ }
                throw;
            }
        }
        catch (Exception ex)
        {
            return new TableDesignSaveResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    #region Script Building

    private static List<string> BuildCreateScripts(DbScriptGenerator scriptGenerator, TableDesignInfo design)
    {
        var table = ToTable(design);
        var columns = design.Columns.Select(ToTableColumn).ToList();
        var pk = design.PrimaryKey is not null ? ToPrimaryKey(design) : null;
        var fks = design.ForeignKeys.Select(fk => ToForeignKey(design, fk)).ToList();
        var indexes = design.Indexes.Select(idx => ToIndex(design, idx)).ToList();
        var constraints = design.Constraints.Select(c => ToConstraint(design, c)).ToList();

        var builder = scriptGenerator.CreateTable(table, columns, pk, fks, indexes, constraints);

        var full = builder.ToString();
        if (string.IsNullOrWhiteSpace(full))
            return new List<string>();

        // 将完整的 CREATE 脚本作为一条返回（包含表、注释、默认值、主键、外键、索引、约束）。
        return new List<string> { full };
    }

    private static List<string> BuildAlterScripts(
        DbInterpreter interpreter,
        DbScriptGenerator scriptGenerator,
        TableDesignInfo oldDesign,
        TableDesignInfo newDesign)
    {
        var scripts = new List<string>();
        var table = ToTable(newDesign);

        // 表名修改。
        if (!string.Equals(oldDesign.Name, newDesign.Name, StringComparison.OrdinalIgnoreCase))
        {
            var rename = scriptGenerator.RenameTable(ToTable(oldDesign), newDesign.Name);
            AddScript(scripts, rename);
        }

        // 表注释。
        if (!string.Equals(oldDesign.Comment ?? string.Empty, newDesign.Comment ?? string.Empty, StringComparison.Ordinal))
        {
            var setComment = scriptGenerator.SetTableComment(table);
            AddScript(scripts, setComment);
        }

        // 列差异。
        var oldCols = oldDesign.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var newCols = newDesign.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var col in newDesign.Columns)
        {
            // 新增列。
            if (!oldCols.ContainsKey(col.Name))
            {
                AddScript(scripts, scriptGenerator.AddTableColumn(table, ToTableColumn(col)));
            }
            else
            {
                // 修改列（比较关键属性）。
                var oldCol = oldCols[col.Name];
                if (IsColumnChanged(oldCol, col))
                {
                    AddScript(scripts, scriptGenerator.AlterTableColumn(table, ToTableColumn(col), ToTableColumn(oldCol)));
                }
            }
        }

        // 删除列。
        foreach (var oldCol in oldDesign.Columns)
        {
            if (!newCols.ContainsKey(oldCol.Name))
            {
                AddScript(scripts, scriptGenerator.DropTableColumn(ToTableColumn(oldCol)));
            }
        }

        // 主键差异。
        BuildPrimaryKeyScripts(scriptGenerator, oldDesign, newDesign, scripts);

        // 索引差异。
        BuildIndexScripts(scriptGenerator, oldDesign, newDesign, scripts);

        // 外键差异。
        BuildForeignKeyScripts(scriptGenerator, oldDesign, newDesign, scripts);

        // 约束差异。
        BuildConstraintScripts(scriptGenerator, oldDesign, newDesign, scripts);

        return scripts;
    }

    private static void BuildPrimaryKeyScripts(
        DbScriptGenerator scriptGenerator,
        TableDesignInfo oldDesign,
        TableDesignInfo newDesign,
        List<string> scripts)
    {
        var oldPk = oldDesign.PrimaryKey;
        var newPk = newDesign.PrimaryKey;

        if (oldPk is null && newPk is null)
            return;

        bool same = oldPk is not null && newPk is not null
            && oldPk.Columns.SequenceEqual(newPk.Columns, StringComparer.OrdinalIgnoreCase)
            && oldPk.Clustered == newPk.Clustered;

        if (same)
            return;

        // 有旧主键先删除，再按新定义添加。
        if (oldPk is not null)
        {
            AddScript(scripts, scriptGenerator.DropPrimaryKey(ToPrimaryKey(oldDesign)));
        }

        if (newPk is not null && newPk.Columns.Count > 0)
        {
            AddScript(scripts, scriptGenerator.AddPrimaryKey(ToPrimaryKey(newDesign)));
        }
    }

    private static void BuildIndexScripts(
        DbScriptGenerator scriptGenerator,
        TableDesignInfo oldDesign,
        TableDesignInfo newDesign,
        List<string> scripts)
    {
        var oldIndexes = oldDesign.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        var newIndexes = newDesign.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

        // 新增 / 修改索引。
        foreach (var idx in newDesign.Indexes)
        {
            if (string.IsNullOrWhiteSpace(idx.Name))
                continue;

            if (!oldIndexes.ContainsKey(idx.Name) || !IsIndexSame(oldIndexes[idx.Name], idx))
            {
                if (oldIndexes.ContainsKey(idx.Name))
                {
                    AddScript(scripts, scriptGenerator.DropIndex(ToIndex(oldDesign, oldIndexes[idx.Name])));
                }

                AddScript(scripts, scriptGenerator.AddIndex(ToIndex(newDesign, idx)));
            }
        }

        // 删除索引。
        foreach (var oldIdx in oldDesign.Indexes)
        {
            if (string.IsNullOrWhiteSpace(oldIdx.Name))
                continue;

            if (!newIndexes.ContainsKey(oldIdx.Name))
            {
                AddScript(scripts, scriptGenerator.DropIndex(ToIndex(oldDesign, oldIdx)));
            }
        }
    }

    private static void BuildForeignKeyScripts(
        DbScriptGenerator scriptGenerator,
        TableDesignInfo oldDesign,
        TableDesignInfo newDesign,
        List<string> scripts)
    {
        var oldFks = oldDesign.ForeignKeys.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        var newFks = newDesign.ForeignKeys.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var fk in newDesign.ForeignKeys)
        {
            if (string.IsNullOrWhiteSpace(fk.Name))
                continue;

            if (!oldFks.ContainsKey(fk.Name) || !IsForeignKeySame(oldFks[fk.Name], fk))
            {
                if (oldFks.ContainsKey(fk.Name))
                {
                    AddScript(scripts, scriptGenerator.DropForeignKey(ToForeignKey(oldDesign, oldFks[fk.Name])));
                }

                AddScript(scripts, scriptGenerator.AddForeignKey(ToForeignKey(newDesign, fk)));
            }
        }

        foreach (var oldFk in oldDesign.ForeignKeys)
        {
            if (string.IsNullOrWhiteSpace(oldFk.Name))
                continue;

            if (!newFks.ContainsKey(oldFk.Name))
            {
                AddScript(scripts, scriptGenerator.DropForeignKey(ToForeignKey(oldDesign, oldFk)));
            }
        }
    }

    private static void BuildConstraintScripts(
        DbScriptGenerator scriptGenerator,
        TableDesignInfo oldDesign,
        TableDesignInfo newDesign,
        List<string> scripts)
    {
        var oldC = oldDesign.Constraints.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var newC = newDesign.Constraints.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var c in newDesign.Constraints)
        {
            if (string.IsNullOrWhiteSpace(c.Name))
                continue;

            if (!oldC.ContainsKey(c.Name) || !string.Equals(oldC[c.Name].Definition, c.Definition, StringComparison.OrdinalIgnoreCase))
            {
                if (oldC.ContainsKey(c.Name))
                {
                    AddScript(scripts, scriptGenerator.DropCheckConstraint(ToConstraint(oldDesign, oldC[c.Name])));
                }

                AddScript(scripts, scriptGenerator.AddCheckConstraint(ToConstraint(newDesign, c)));
            }
        }

        foreach (var oldCc in oldDesign.Constraints)
        {
            if (string.IsNullOrWhiteSpace(oldCc.Name))
                continue;

            if (!newC.ContainsKey(oldCc.Name))
            {
                AddScript(scripts, scriptGenerator.DropCheckConstraint(ToConstraint(oldDesign, oldCc)));
            }
        }
    }

    private static bool IsColumnChanged(TableDesignColumn oldCol, TableDesignColumn newCol)
    {
        return !string.Equals(oldCol.DataType, newCol.DataType, StringComparison.OrdinalIgnoreCase)
            || oldCol.MaxLength != newCol.MaxLength
            || oldCol.Precision != newCol.Precision
            || oldCol.Scale != newCol.Scale
            || oldCol.IsNullable != newCol.IsNullable
            || oldCol.IsIdentity != newCol.IsIdentity
            || !string.Equals(oldCol.DefaultValue ?? string.Empty, newCol.DefaultValue ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool IsIndexSame(TableDesignIndex a, TableDesignIndex b)
    {
        return a.IsUnique == b.IsUnique
            && a.Columns.SequenceEqual(b.Columns, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsForeignKeySame(TableDesignForeignKey a, TableDesignForeignKey b)
    {
        return a.UpdateCascade == b.UpdateCascade
            && a.DeleteCascade == b.DeleteCascade
            && string.Equals(a.ReferencedTableName, b.ReferencedTableName, StringComparison.OrdinalIgnoreCase)
            && a.Columns.Count == b.Columns.Count
            && a.Columns.Zip(b.Columns, (x, y) =>
                string.Equals(x.ColumnName, y.ColumnName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.ReferencedColumnName, y.ReferencedColumnName, StringComparison.OrdinalIgnoreCase))
                .All(ok => ok);
    }

    private static void AddScript(List<string> scripts, Script script)
    {
        if (script is null)
            return;

        var content = script.Content;
        if (string.IsNullOrWhiteSpace(content))
            return;

        content = content.Trim();
        if (string.IsNullOrWhiteSpace(content) || content == ";")
            return;

        scripts.Add(content);
    }

    private static List<string> SplitStatements(string script, string delimiter)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(script))
            return result;

        string sep = string.IsNullOrEmpty(delimiter) ? ";" : delimiter;

        // 按分隔符拆分，保留内容。
        var parts = script.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;
            result.Add(trimmed.TrimEnd(';'));
        }

        return result;
    }

    #endregion

    #region Model Mapping

    private static Table ToTable(TableDesignInfo design)
    {
        return new Table
        {
            Schema = design.Schema,
            Name = design.Name,
            Comment = design.Comment,
        };
    }

    private static TableColumn ToTableColumn(TableDesignColumn col)
    {
        return new TableColumn
        {
            Name = col.Name,
            DataType = col.DataType,
            MaxLength = col.MaxLength,
            Precision = col.Precision,
            Scale = col.Scale,
            IsNullable = col.IsNullable,
            IsIdentity = col.IsIdentity,
            DefaultValue = string.IsNullOrEmpty(col.DefaultValue) ? null : col.DefaultValue,
            ComputeExp = col.ComputeExp,
            Comment = col.Comment,
        };
    }

    private static TablePrimaryKey ToPrimaryKey(TableDesignInfo design)
    {
        var pk = design.PrimaryKey ?? new TableDesignKey();
        return new TablePrimaryKey
        {
            Schema = design.Schema,
            TableName = design.Name,
            Name = string.IsNullOrEmpty(pk.Name) ? $"PK_{design.Name}" : pk.Name,
            Clustered = pk.Clustered,
            Columns = pk.Columns.Select(c => new IndexColumn { ColumnName = c }).ToList(),
        };
    }

    private static TableIndex ToIndex(TableDesignInfo design, TableDesignIndex idx)
    {
        return new TableIndex
        {
            Schema = design.Schema,
            TableName = design.Name,
            Name = idx.Name,
            IsUnique = idx.IsUnique,
            Columns = idx.Columns.Select(c => new IndexColumn { ColumnName = c }).ToList(),
        };
    }

    private static TableForeignKey ToForeignKey(TableDesignInfo design, TableDesignForeignKey fk)
    {
        return new TableForeignKey
        {
            Schema = design.Schema,
            TableName = design.Name,
            Name = fk.Name,
            ReferencedSchema = fk.ReferencedSchema,
            ReferencedTableName = fk.ReferencedTableName,
            UpdateCascade = fk.UpdateCascade,
            DeleteCascade = fk.DeleteCascade,
            Columns = fk.Columns.Select(c => new ForeignKeyColumn
            {
                ColumnName = c.ColumnName,
                ReferencedColumnName = c.ReferencedColumnName,
            }).ToList(),
        };
    }

    private static TableConstraint ToConstraint(TableDesignInfo design, TableDesignConstraint c)
    {
        return new TableConstraint
        {
            Schema = design.Schema,
            TableName = design.Name,
            Name = c.Name,
            Definition = c.Definition,
        };
    }

    #endregion

    private DbInterpreter CreateInterpreter(ConnectionItem connection, string? databaseOverride = null)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);

        var connectionInfo = new ConnectionInfo
        {
            Server = connection.Server,
            Port = connection.Port,
            ServerVersion = connection.ServerVersion,
            Database = string.IsNullOrEmpty(databaseOverride) ? connection.Database : databaseOverride,
            IntegratedSecurity = connection.IntegratedSecurity,
            UserId = connection.UserId,
            Password = connection.Password,
            IsDba = connection.IsDba,
            UseSsl = connection.UseSsl,
        };

        var option = new DbInterpreterOption
        {
            ObjectFetchMode = DatabaseObjectFetchMode.Details,
            ShowTextForGeometry = true,
        };

        return DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);
    }

    private ConnectionItem? FindConnection(string connectionName)
        => _connectionService.GetConnections().FirstOrDefault(c =>
            string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));

    private static DatabaseType ParseDatabaseType(string databaseType)
    {
        if (Enum.TryParse<DatabaseType>(databaseType, true, out var type))
            return type;
        return DatabaseType.Unknown;
    }
}
