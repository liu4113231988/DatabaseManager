using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DatabaseInterpreter.Core
{
    /// <summary>
    /// Native AOT 可用的结果集映射器：只使用 <see cref="PropertyInfo"/> 读写，不生成运行时 IL，
    /// 用于替代 Dapper 的 <c>QueryAsync&lt;T&gt;</c> 对象映射。列名与公共可写属性的匹配规则与 Dapper
    /// 默认行为一致：先同名精确匹配，再忽略大小写；无匹配列的属性保持初始值；DBNull 对值类型取默认值、
    /// 对可空与引用类型取 null。
    /// </summary>
    public static class DbRowMapper
    {
        private static readonly ConcurrentDictionary<string, MapEntry[]> Plans = new ConcurrentDictionary<string, MapEntry[]>();

        private static readonly object Unassigned = new object();

        /// <summary>构造参数列表，便于在调用处内联传参。</summary>
        public static IList<(string Name, object Value)> Params(params (string Name, object Value)[] parameters)
            => parameters ?? Array.Empty<(string Name, object Value)>();

        /// <summary>逐行构造对象列表。</summary>
        public static async Task<List<T>> ReadObjectsAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
            DbConnection connection, string sql, Func<DbConnection, Task> openConnection = null,
            IList<(string Name, object Value)> parameters = null) where T : new()
        {
            var results = new List<T>();

            if (string.IsNullOrEmpty(sql))
            {
                return results;
            }

            using (DbCommand command = CreateCommand(connection, sql, parameters))
            {
                await EnsureOpenAsync(connection, openConnection);

                using (DbDataReader reader = await command.ExecuteReaderAsync())
                {
                    MapEntry[] plan = GetPlan<T>(reader);

                    while (await reader.ReadAsync())
                    {
                        results.Add(ReadObject<T>(reader, plan));
                    }
                }
            }

            return results;
        }

        /// <summary>读取每行第一列的值。</summary>
        public static async Task<List<T>> ReadColumnValuesAsync<T>(DbConnection connection, string sql,
            Func<DbConnection, Task> openConnection = null, IList<(string Name, object Value)> parameters = null)
        {
            var results = new List<T>();

            if (string.IsNullOrEmpty(sql))
            {
                return results;
            }

            using (DbCommand command = CreateCommand(connection, sql, parameters))
            {
                await EnsureOpenAsync(connection, openConnection);

                using (DbDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(ReadValue<T>(reader, 0));
                    }
                }
            }

            return results;
        }

        /// <summary>读取首行首列的值，无结果行时返回默认值。</summary>
        public static async Task<T> ReadFirstValueAsync<T>(DbConnection connection, string sql,
            Func<DbConnection, Task> openConnection = null, IList<(string Name, object Value)> parameters = null)
        {
            List<T> values = await ReadColumnValuesAsync<T>(connection, sql, openConnection, parameters);

            return values.Count == 0 ? default : values[0];
        }

        /// <summary>读取首行首列的原始值，无结果行时返回 null。</summary>
        public static async Task<object> ReadScalarAsync(DbConnection connection, string sql,
            Func<DbConnection, Task> openConnection = null, IList<(string Name, object Value)> parameters = null)
        {
            List<object> values = await ReadColumnValuesAsync<object>(connection, sql, openConnection, parameters);

            return values.Count == 0 ? null : values[0];
        }

        /// <summary>执行标量查询并转换为指定类型，无结果行或 DBNull 时返回默认值。</summary>
        public static async Task<T> ExecuteScalarAsync<T>(DbConnection connection, string sql,
            Func<DbConnection, Task> openConnection = null, IList<(string Name, object Value)> parameters = null)
        {
            object value = await ReadScalarAsync(connection, sql, openConnection, parameters);

            if (value == null)
            {
                return default;
            }

            return (T)ConvertValue(value, typeof(T));
        }

        /// <summary>执行非查询命令（替代 Dapper 的 <c>ExecuteAsync</c>）。</summary>
        public static async Task<int> ExecuteNonQueryAsync(DbConnection connection, string sql,
            Func<DbConnection, Task> openConnection = null, IList<(string Name, object Value)> parameters = null,
            DbTransaction transaction = null)
        {
            using (DbCommand command = CreateCommand(connection, sql, parameters))
            {
                if (transaction != null)
                {
                    command.Transaction = transaction;
                }

                await EnsureOpenAsync(connection, openConnection);

                return await command.ExecuteNonQueryAsync();
            }
        }

        /// <summary>创建命令并按原样附加参数名（保留 @ / : 等驱动前缀写法）。</summary>
        public static DbCommand CreateCommand(DbConnection connection, string sql, IEnumerable<(string Name, object Value)> parameters)
        {
            DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            foreach ((string name, object value) in parameters ?? Enumerable.Empty<(string Name, object Value)>())
            {
                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }

            return command;
        }

        private static async Task EnsureOpenAsync(DbConnection connection, Func<DbConnection, Task> openConnection)
        {
            if (connection.State == ConnectionState.Closed)
            {
                if (openConnection != null)
                {
                    await openConnection(connection);
                }
                else
                {
                    await connection.OpenAsync();
                }
            }
        }

        private static T ReadObject<T>(DbDataReader reader, MapEntry[] plan) where T : new()
        {
            var result = new T();

            foreach (MapEntry entry in plan)
            {
                object value = reader.IsDBNull(entry.Ordinal) ? null : reader.GetValue(entry.Ordinal);

                object converted = ConvertValue(value, entry.Property.PropertyType);

                if (!ReferenceEquals(converted, Unassigned))
                {
                    entry.Property.SetValue(result, converted);
                }
            }

            return result;
        }

        private static T ReadValue<T>(DbDataReader reader, int ordinal)
        {
            object value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);

            if (value == null)
            {
                return default;
            }

            if (value is T typed)
            {
                return typed;
            }

            return (T)ConvertValue(value, typeof(T));
        }

        private static MapEntry[] GetPlan<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(DbDataReader reader)
        {
            string[] columnNames = new string[reader.FieldCount];

            for (int i = 0; i < columnNames.Length; i++)
            {
                columnNames[i] = reader.GetName(i);
            }

            string key = typeof(T).FullName + "|" + string.Join(";", columnNames);

            return Plans.GetOrAdd(key, _ => BuildPlan(typeof(T), columnNames));
        }

        private static MapEntry[] BuildPlan([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, string[] columnNames)
        {
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(item => item.CanWrite && item.GetIndexParameters().Length == 0 && IsMappable(item.PropertyType))
                .ToArray();

            var entries = new List<MapEntry>(columnNames.Length);
            var mappedProperties = new HashSet<PropertyInfo>();

            foreach (string columnName in columnNames)
            {
                string name = (columnName ?? string.Empty).Trim();

                PropertyInfo property = properties.FirstOrDefault(item => item.Name == name)
                    ?? properties.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

                if (property is null || !mappedProperties.Add(property))
                {
                    continue;
                }

                entries.Add(new MapEntry(property, Array.IndexOf(columnNames, columnName)));
            }

            return entries.ToArray();
        }

        /// <summary>仅映射标量类型：与 Dapper 一致，复杂类型（列表、嵌套对象）保持初始值。</summary>
        private static bool IsMappable(Type propertyType)
        {
            Type type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (type == typeof(object) || type == typeof(string) || type == typeof(byte[]) || type.IsEnum)
            {
                return true;
            }

            return type.IsValueType && (type.IsPrimitive || type == typeof(decimal) || type == typeof(DateTime)
                || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid));
        }

        private static object ConvertValue(object value, Type targetType)
        {
            Type underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (value == null || value is DBNull)
            {
                // 非空值类型保持属性初始值：驱动返回 DBNull 的列极少出现在带初始值的属性上（仅 Clustered）。
                return targetType.IsValueType && underlying == targetType ? Unassigned : null;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (underlying == typeof(string))
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (underlying == typeof(byte[]))
            {
                return value;
            }

            if (underlying.IsEnum)
            {
                if (value is string text)
                {
                    return Enum.Parse(underlying, text, ignoreCase: true);
                }

                return Enum.ToObject(underlying, Convert.ChangeType(value, Enum.GetUnderlyingType(underlying), CultureInfo.InvariantCulture));
            }

            if (underlying == typeof(Guid))
            {
                if (value is string guidText)
                {
                    return Guid.Parse(guidText);
                }

                if (value is byte[] bytes)
                {
                    return new Guid(bytes);
                }
            }

            if (underlying == typeof(TimeSpan) && value is not TimeSpan)
            {
                if (value is DateTime dateTime)
                {
                    return dateTime.TimeOfDay;
                }

                return TimeSpan.FromSeconds(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            }

            if (underlying == typeof(bool) && value is not bool)
            {
                return ToBoolean(value);
            }

            if (underlying == typeof(char) && value is string chars && chars.Length > 0)
            {
                return chars[0];
            }

            if (underlying == typeof(DateTimeOffset) && value is DateTime moment)
            {
                return new DateTimeOffset(moment);
            }

            if (underlying == typeof(DateTime) && value is DateTimeOffset datedMoment)
            {
                return datedMoment.DateTime;
            }

            if (underlying.IsValueType && value is IConvertible)
            {
                return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
            }

            return value;
        }

        private static bool ToBoolean(object value)
        {
            if (value is string text)
            {
                string trimmed = text.Trim();

                if (trimmed.Length == 0)
                {
                    return false;
                }

                if (trimmed == "1" || string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "t", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "Y", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (trimmed == "0" || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "f", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "no", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (value is IConvertible convertible)
            {
                return Convert.ToBoolean(convertible, CultureInfo.InvariantCulture);
            }

            return false;
        }

        private sealed class MapEntry
        {
            public MapEntry(PropertyInfo property, int ordinal)
            {
                Property = property;
                Ordinal = ordinal;
            }

            public PropertyInfo Property { get; }

            public int Ordinal { get; }
        }
    }
}
