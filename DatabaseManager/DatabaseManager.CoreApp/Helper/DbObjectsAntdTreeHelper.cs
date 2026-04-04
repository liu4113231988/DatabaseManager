using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core;
using DatabaseManager.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DatabaseManager.Helper
{
    public static class DbObjectsAntdTreeHelper
    {
        public static readonly string FakeNodeName = "_FakeNode_";
        public static DatabaseObjectType DefaultObjectType = DatabaseObjectType.Type | DatabaseObjectType.Sequence | DatabaseObjectType.Table
                                                           | DatabaseObjectType.View | DatabaseObjectType.Procedure | DatabaseObjectType.Function | DatabaseObjectType.Trigger;

        private static Dictionary<string, Image> iconCache = new Dictionary<string, Image>();

        public static string GetFolderNameByDbObjectType(DatabaseObjectType databaseObjectType)
        {
            return ManagerUtil.GetPluralString(databaseObjectType.ToString());
        }

        public static string GetFolderNameByDbObjectType(Type objType)
        {
            return ManagerUtil.GetPluralString(objType.Name);
        }

        public static DatabaseObjectType GetDbObjectTypeByFolderName(string folderName)
        {
            if (folderName == DbObjectTreeFolderType.Types.ToString())
            {
                return DatabaseObjectType.Type;
            }

            string value = ManagerUtil.GetSingularString(folderName);
            DatabaseObjectType type = DatabaseObjectType.None;

            Enum.TryParse(value, out type);

            return type;
        }

        public static string GetIconName(string name)
        {
            return $"tree_{name}";
        }

        public static Image GetIconFromResources(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
            {
                return null;
            }

            if (iconCache.ContainsKey(iconName))
            {
                return iconCache[iconName];
            }

            try
            {
                string resourceKey = GetIconName(iconName);
                var image = (Image)Resources.ResourceManager.GetObject(resourceKey);
                if (image != null)
                {
                    iconCache[iconName] = image;
                    return image;
                }
            }
            catch
            {
            }

            return null;
        }

        public static AntdUI.TreeItem CreateTreeItem(string name, string text, string iconKeyName)
        {
            var item = new AntdUI.TreeItem(text);
            item.Name = name;
            
            var icon = GetIconFromResources(iconKeyName);
            if (icon != null)
            {
                item.Icon = icon;
            }
            
            return item;
        }

        public static AntdUI.TreeItem CreateFolderItem(string name, string text, bool createFakeNode = false)
        {
            var item = CreateTreeItem(name, text, "Folder");

            if (createFakeNode)
            {
                item.Sub.Add(CreateFakeItem());
            }

            return item;
        }

        public static AntdUI.TreeItem CreateFakeItem()
        {
            return CreateTreeItem(FakeNodeName, "", "Fake");
        }

        public static AntdUI.TreeItem CreateTreeItem<T>(T dbObject, bool createFakeNode = false)
            where T : DatabaseObject
        {
            var item = CreateTreeItem(dbObject.Name, dbObject.Name, dbObject.GetType().Name);
            item.Tag = dbObject;

            if (createFakeNode)
            {
                item.Sub.Add(CreateFakeItem());
            }

            return item;
        }

        public static void AddDbObjectItems<T>(this AntdUI.TreeItem treeItem, IEnumerable<T> dbObjects, bool showSchema = false)
            where T : DatabaseObject
        {
            foreach (var item in CreateDbObjectItems(dbObjects, showSchema))
            {
                treeItem.Sub.Add(item);
            }
        }

        public static AntdUI.TreeItem AddDbObjectFolderItem<T>(this AntdUI.TreeItem treeItem, IEnumerable<T> dbObjects, bool showSchema = false)
            where T : DatabaseObject
        {
            string folderName = GetFolderNameByDbObjectType(typeof(T));

            var node = CreateFolderItem(folderName, folderName, dbObjects, showSchema);
            if (node != null)
            {
                treeItem.Sub.Add(node);
                return node;
            }

            return null;
        }

        public static AntdUI.TreeItem AddDbObjectFolderItem<T>(this AntdUI.TreeItemCollection items, string name, string text, List<T> dbObjects, bool showSchema = false)
            where T : DatabaseObject
        {
            var node = CreateFolderItem(name, text, dbObjects, showSchema);

            if (node != null)
            {
                items.Add(node);
                return node;
            }

            return null;
        }

        public static AntdUI.TreeItem CreateFolderItem<T>(string name, string text, IEnumerable<T> dbObjects, bool showSchema = false)
            where T : DatabaseObject
        {
            if (dbObjects.Count() > 0)
            {
                var item = CreateFolderItem(name, text);

                foreach (var childItem in CreateDbObjectItems(dbObjects, showSchema))
                {
                    item.Sub.Add(childItem);
                }

                return item;
            }
            return null;
        }

        public static IEnumerable<AntdUI.TreeItem> CreateDbObjectItems<T>(IEnumerable<T> dbObjects, bool showSchema = false)
            where T : DatabaseObject
        {
            bool isUniqueDbSchema = dbObjects.GroupBy(item => item.Schema).Count() == 1;

            if (!isUniqueDbSchema)
            {
                dbObjects = dbObjects.OrderBy(item => item.Schema).ThenBy(item => item.Name);
            }

            foreach (var dbObj in dbObjects)
            {
                string text = showSchema ? $"{dbObj.Schema}{(string.IsNullOrEmpty(dbObj.Schema) ? "" : ".")}{dbObj.Name}" : dbObj.Name;

                string imgKeyName = typeof(T).Name;

                if (dbObj is Function func)
                {
                    if (func.IsTriggerFunction)
                    {
                        imgKeyName = $"{imgKeyName}_Trigger";
                    }
                }

                var item = CreateTreeItem(dbObj.Name, text, imgKeyName);
                item.Tag = dbObj;

                yield return item;
            }
        }

        public static bool NeedShowSchema<T>(DbInterpreter dbInterpreter, IEnumerable<T> dbObjects) where T : DatabaseObject
        {
            string defaultSchema = dbInterpreter?.DefaultSchema;

            if (!string.IsNullOrEmpty(defaultSchema))
            {
                return !dbObjects.All(item => item.Schema == defaultSchema) && !dbObjects.All(item => string.IsNullOrEmpty(item.Schema));
            }
            else
            {
                return dbObjects.GroupBy(item => item.Schema).Count() > 1;
            }
        }

        public static AntdUI.TreeItem FindItemByTag(this AntdUI.TreeItemCollection items, object tag)
        {
            foreach (var item in items)
            {
                if (item.Tag == tag)
                    return item;

                var found = item.Sub.FindItemByTag(tag);
                if (found != null)
                    return found;
            }
            return null;
        }

        public static AntdUI.TreeItem FindItemByName(this AntdUI.TreeItemCollection items, string name)
        {
            foreach (var item in items)
            {
                if (item.Name == name)
                    return item;

                var found = item.Sub.FindItemByName(name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
