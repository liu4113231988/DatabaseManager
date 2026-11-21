using System;
using System.IO;
using System.Text.Json;

namespace DatabaseManager.Core
{
    internal static class CoreModelCopy
    {
        public static T Copy<T>(object source)
        {
            if (source == null)
            {
                return default;
            }

            Type sourceType = source.GetType();
            var sourceTypeInfo = CoreJsonContext.Default.GetTypeInfo(sourceType);
            if (sourceTypeInfo == null)
            {
                throw new NotSupportedException($"Type {sourceType.FullName} is not registered for AOT-safe copying.");
            }

            Type targetType = typeof(T);
            var targetTypeInfo = CoreJsonContext.Default.GetTypeInfo(targetType);
            if (targetTypeInfo == null)
            {
                throw new NotSupportedException($"Type {targetType.FullName} is not registered for AOT-safe copying.");
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                JsonSerializer.Serialize(writer, source, sourceTypeInfo);
            }

            return (T)JsonSerializer.Deserialize(stream.ToArray(), targetTypeInfo);
        }
    }
}
