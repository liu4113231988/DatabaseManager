using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core.Model;
using DatabaseManager.FileUtility;
using DatabaseManager.FileUtility.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Table = DatabaseInterpreter.Model.Table;

namespace DatabaseManager.Core
{
    public class DataExporter
    {
        private IObserver<FeedbackInfo> observer;

        public void Subscribe(IObserver<FeedbackInfo> observer)
        {
            this.observer = observer;
        }

        public async Task<DataExportResult> Export(DbInterpreter dbInterpreter, DatabaseObject dbObject, List<DataExportColumn> columns, ExportSpecificDataOption option, CancellationToken cancellationToken)
        {
            DataExportResult exportResult = new DataExportResult();

            List<TableColumn> tableColumns = new List<TableColumn>();

            if (columns != null)
            {
                tableColumns.AddRange(columns.Select(item => new TableColumn() { Name = item.ColumnName, DataType = item.DataType }));
            }

            bool isForView = false;

            if (dbObject is View)
            {
                dbObject = ObjectHelper.CloneObject<Table>(dbObject);

                isForView = true;
            }

            try
            {
                DataTable mergedDataTable = new DataTable();

                using (var connection = dbInterpreter.CreateConnection())
                {
                    if (!option.ExportAllThatMeetCondition)
                    {
                        List<long> pageNumbers = option.PageNumbers;

                        foreach (long pageNumber in pageNumbers)
                        {
                            (long Total, DataTable Data) result = await dbInterpreter.GetPagedDataTableAsync(connection, dbObject as Table, option.OrderColumns, option.PageSize, pageNumber, option.ConditionClause, isForView, tableColumns);

                            mergedDataTable.Merge(result.Data);
                        }
                    }
                    else
                    {
                        int batchCount = option.PageSize > 0 ? option.PageSize : 500;
                        // 与 DefaultExportImportService 保持一致的分批上限，避免内存峰值过大
                        if (batchCount > 1000) batchCount = 1000;
                        long startPage = Math.Max(1, option.StartPageNumber);
                        long count = (startPage - 1) * batchCount;

                        if (startPage > 1)
                        {
                            this.Feedback($"Resuming export from page {startPage}...");
                        }

                        (long Total, DataTable Data) result = await dbInterpreter.GetPagedDataTableAsync(connection, dbObject as Table, option.OrderColumns, batchCount, startPage, option.ConditionClause, isForView, tableColumns);

                        count += result.Data.Rows.Count;

                        mergedDataTable.Merge(result.Data);

                        long total = result.Total;

                        long pageNumber = total % batchCount == 0 ? total / batchCount : total / batchCount + 1;

                        if (pageNumber > startPage)
                        {
                            for (long i = startPage + 1; i <= pageNumber; i++)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    exportResult.Message = $"Task has been canceled. (completed to page {i - 1})";
                                    break;
                                }

                                count += (i < pageNumber ? batchCount : total - (pageNumber - 1) * batchCount);

                                this.Feedback($"Reading data {count}/{result.Total}...");

                                result = await dbInterpreter.GetPagedDataTableAsync(connection, dbObject as Table, option.OrderColumns, batchCount, i, option.ConditionClause, isForView, tableColumns);

                                mergedDataTable.Merge(result.Data);
                            }
                        }
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        this.Feedback("Writing to file...");

                        if (columns != null)
                        {
                            foreach (DataColumn column in mergedDataTable.Columns)
                            {
                                var col = columns.FirstOrDefault(item => item.ColumnName == column.ColumnName);

                                if (col != null && col.ColumnName != col.DisplayName && !string.IsNullOrEmpty(col.DisplayName))
                                {
                                    column.ColumnName = col.DisplayName;
                                }
                            }
                        }

                        string filePath = this.ExportDataTable(mergedDataTable, dbObject.Name, option, dbInterpreter);

                        this.Feedback("End write to file.");

                        exportResult.IsOK = true;
                        exportResult.FilePath = filePath;

                        return exportResult;
                    }
                    else
                    {
                        return exportResult;
                    }
                }
            }
            catch (Exception ex)
            {
                this.HandleError(ex);

                exportResult.Message = ex.Message;

                return exportResult;
            }
        }

        public DataExportResult Export(DataTable dataTable, List<DataExportColumn> columns, ExportSpecificDataOption option)
        {
            DataExportResult exportResult = new DataExportResult();

            List<DataColumn> excludeColumns = new List<DataColumn>();

            foreach (DataColumn column in dataTable.Columns)
            {
                string columnName = column.ColumnName;

                var col = columns.FirstOrDefault(item => item.ColumnName == columnName);

                if (col != null)
                {
                    if(!string.IsNullOrEmpty(col.DisplayName))
                    {
                        column.ColumnName = col.DisplayName;
                    }                   
                }
                else
                {
                    excludeColumns.Add(column);
                }
            }

            excludeColumns.ForEach(item => { dataTable.Columns.Remove(item); });

            exportResult.FilePath = this.ExportDataTable(dataTable, dataTable.TableName, option);
            exportResult.IsOK = true;

            return exportResult;
        }

        private string ExportDataTable(DataTable dataTable, string tableName, ExportDataOption option, DbInterpreter dbInterpreter = null)
        {
            string filePath = null;

            if (option.FileType == ExportFileType.CSV)
            {
                filePath = this.WriteToCsv(dataTable, option, tableName);
            }
            else if (option.FileType == ExportFileType.JSON)
            {
                filePath = new JsonDataWriter(option).Write(dataTable, tableName);
            }
            else if (option.FileType == ExportFileType.XML)
            {
                filePath = new XmlDataWriter(option).Write(dataTable, tableName);
            }
            else if (option.FileType == ExportFileType.SQL)
            {
                filePath = this.WriteToSql(dataTable, option, tableName, dbInterpreter);
            }
            else
            {
                filePath = this.WriteToExcel(dataTable, option, tableName);
            }

            return filePath;
        }

        /// <summary>把 DataTable 写出为 INSERT 语句脚本（SQL 格式导出）。</summary>
        private string WriteToSql(DataTable dataTable, ExportDataOption option, string tableName, DbInterpreter dbInterpreter)
        {
            if (dbInterpreter == null)
            {
                throw new NotSupportedException("SQL export requires a database interpreter.");
            }

            string filePath = option.FilePath;

            if (string.IsNullOrEmpty(filePath))
            {
                string folder = option.IsTemporary ? "temp" : "export";

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder, $"{tableName}_{DateTime.Now:yyyyMMdd}.sql");
            }

            dbInterpreter.Option.ScriptOutputMode = GenerateScriptOutputMode.WriteToString;

            var scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(dbInterpreter);
            var table = new Table() { Name = tableName };
            var columns = dataTable.Columns.Cast<DataColumn>()
                .Select(c => new TableColumn() { Name = c.ColumnName, DataType = MapToSqlDataType(c.DataType) })
                .ToList();

            var rows = dbInterpreter.ConvertDataTableToDictionaryList(dataTable, columns);

            var sb = new StringBuilder();
            sb.AppendLine($"-- Exported from table {tableName} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            int batchCount = 500;
            long pageCount = rows.Count == 0 ? 0 : (rows.Count % batchCount == 0 ? rows.Count / batchCount : rows.Count / batchCount + 1);

            var dictPagedData = new Dictionary<long, List<Dictionary<string, object>>>();

            for (int i = 0; i < pageCount; i++)
            {
                dictPagedData[i + 1] = rows.Skip(i * batchCount).Take(batchCount).ToList();
            }

            scriptGenerator.AppendDataScripts(sb, table, columns, dictPagedData);

            var encoding = TextEncoding.Resolve(option.EncodingName) ?? Encoding.UTF8;

            File.WriteAllText(filePath, sb.ToString(), encoding);

            return filePath;
        }

        /// <summary>.NET 类型 → 通用 SQL 类型（用于 SQL 导出时生成 INSERT 的值转义）。</summary>
        private static string MapToSqlDataType(Type type)
        {
            if (type == typeof(string) || type == typeof(Guid) || type == typeof(char))
            {
                return "VARCHAR";
            }
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan))
            {
                return "DATETIME";
            }
            if (type == typeof(bool))
            {
                return "INT";
            }
            if (type == typeof(byte[]))
            {
                return "BLOB";
            }
            if (type == typeof(long) || type == typeof(ulong))
            {
                return "BIGINT";
            }
            if (type == typeof(short) || type == typeof(ushort))
            {
                return "SMALLINT";
            }
            if (type == typeof(decimal))
            {
                return "DECIMAL";
            }
            if (type == typeof(double) || type == typeof(float))
            {
                return "DOUBLE";
            }

            return "INT";
        }

        public static void WriteToCsv(DataTable dataTable, string filePath)
        {
            CsvWriter writer = new CsvWriter(new ExportDataOption() { FilePath = filePath });

            writer.Write(dataTable);
        }

        public string WriteToCsv(DataTable dataTable, ExportDataOption option = null, string tableName = null)
        {
            CsvWriter writer = new CsvWriter(option);

            return writer.Write(dataTable, tableName);
        }

        public string WriteToExcel(DataTable dataTable, ExportDataOption option = null, string tableName = null)
        {
            ExcelWriter writer = new ExcelWriter(option);

            return writer.Write(dataTable, tableName);
        }

        private void HandleError(Exception ex)
        {
            string errMsg = ExceptionHelper.GetExceptionDetails(ex);
            this.Feedback(this, errMsg, FeedbackInfoType.Error, true);
        }

        private void Feedback(string message)
        {
            this.Feedback(this, message);
        }

        private void Feedback(object owner, string content, FeedbackInfoType infoType = FeedbackInfoType.Info, bool enableLog = true, bool suppressError = false)
        {
            FeedbackInfo info = new FeedbackInfo() { InfoType = infoType, Message = StringHelper.ToSingleEmptyLine(content), Owner = owner };

            FeedbackHelper.Feedback(suppressError ? null : this.observer, info, enableLog);
        }
    }
}
