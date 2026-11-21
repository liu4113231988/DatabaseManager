using DatabaseInterpreter.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;

namespace DatabaseInterpreter.Utility
{
    public class DataTableHelper
    {
        [UnconditionalSuppressMessage("Trimming", "IL2072",
            Justification = "Changed column types are framework primitives or statically referenced provider geometry types; DataColumn only needs their public metadata, which the native AOT graph preserves.")]
        public static DataTable GetChangedDataTable(DataTable dataTable, Dictionary<int, DataTableColumnChangeInfo> changedColumns, Dictionary<(int RowIndex, int ColumnIndex), object> changedValues)
        {
            DataTable dtChanged = dataTable.Clone();

            for (int i = 0; i < dtChanged.Columns.Count; i++)
            {
                if (changedColumns.ContainsKey(i))
                {
                    if (changedColumns[i].MaxLength.HasValue)
                    {
                        dtChanged.Columns[i].MaxLength = changedColumns[i].MaxLength.Value;
                    }

                    dtChanged.Columns[i].DataType = changedColumns[i].Type;
                }
            }

            int rowIndex = 0;

            foreach (DataRow row in dataTable.Rows)
            {
                DataRow r = dtChanged.NewRow();

                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    var value = row[i];

                    if (changedValues.ContainsKey((rowIndex, i)))
                    {
                        r[i] = changedValues[(rowIndex, i)];
                    }
                    else
                    {
                        r[i] = value;
                    }
                }

                dtChanged.Rows.Add(r);

                rowIndex++;
            }

            return dtChanged;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2072",
            Justification = "Reader field types come from statically referenced database providers and framework primitives; their public metadata is preserved in the native AOT graph.")]
        public static void LoadFromReader(DataTable table, IDataReader reader)
        {
            if (table == null || reader == null)
            {
                return;
            }

            int fieldCount = reader.FieldCount;
            var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < fieldCount; i++)
            {
                string columnName = reader.GetName(i);
                if (string.IsNullOrEmpty(columnName))
                {
                    columnName = "Column" + (i + 1);
                }

                if (columnNames.Contains(columnName))
                {
                    string baseName = columnName;
                    int suffix = 1;
                    while (columnNames.Contains(baseName + suffix))
                    {
                        suffix++;
                    }

                    columnName = baseName + suffix;
                }

                columnNames.Add(columnName);
                table.Columns.Add(columnName, GetColumnType(reader, i));
            }

            object[] values = new object[fieldCount];

            while (reader.Read())
            {
                for (int i = 0; i < fieldCount; i++)
                {
                    values[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                }

                table.Rows.Add(values);
            }
        }

        private static Type GetColumnType(IDataReader reader, int ordinal)
        {
            Type type = reader.GetFieldType(ordinal);

            if (type == null || type == typeof(DBNull))
            {
                return typeof(object);
            }

            return type;
        }
    }
}
