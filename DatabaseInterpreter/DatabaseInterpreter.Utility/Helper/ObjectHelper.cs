using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace DatabaseInterpreter.Utility
{
    public class ObjectHelper
    {
        public static T CloneObject<T>(object obj)
        {
            if (obj == null)
            {
                return default;
            }

            Type sourceType = obj.GetType();
            var sourceTypeInfo = ObjectCloneJsonContext.Default.GetTypeInfo(sourceType);
            if (sourceTypeInfo == null)
            {
                throw new NotSupportedException($"Type {sourceType.FullName} is not registered for AOT-safe cloning.");
            }

            Type targetType = typeof(T);
            var targetTypeInfo = ObjectCloneJsonContext.Default.GetTypeInfo(targetType);
            if (targetTypeInfo == null)
            {
                throw new NotSupportedException($"Type {targetType.FullName} is not registered for AOT-safe cloning.");
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                JsonSerializer.Serialize(writer, obj, sourceTypeInfo);
            }

            return (T)JsonSerializer.Deserialize(stream.ToArray(), targetTypeInfo);
        }

        public static void CopyProperties(object source, object target)
        {
            var sourceProps = source.GetType().GetProperties().Where(x => x.CanRead).ToList();
            var targetProps = target.GetType().GetProperties().Where(x => x.CanWrite).ToList();

            foreach (var sourceProp in sourceProps)
            {
                if (targetProps.Any(x => x.Name == sourceProp.Name))
                {
                    var p = targetProps.FirstOrDefault(x => x.Name == sourceProp.Name);
                    if (p != null && p.CanWrite)
                    {
                        p.SetValue(target, sourceProp.GetValue(source, null), null);
                    }
                }
            }
        }

        public static bool AreObjectsEqual(object object1, object object2)
        {
            if (object1 == null || object2 == null)
            {
                return object1 == object2;
            }

            Type type1 = object1.GetType();
            Type type2 = object2.GetType();

            PropertyInfo[] properties1 = type1.GetProperties();
            PropertyInfo[] properties2 = type1.GetProperties();

            if (properties1.Length != properties2.Length)
            {
                return false;
            }

            foreach (PropertyInfo property in properties1)
            {
                if (!Equals(property.GetValue(object1), property.GetValue(object2)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
