using DatabaseManager.FileUtility.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DatabaseManager.FileUtility
{
    /// <summary>
    /// XML 数据读取器：约定根节点下有重复的记录元素，记录的子元素名为列名，
    /// 如 &lt;rows&gt;&lt;row&gt;&lt;id&gt;1&lt;/id&gt;&lt;/row&gt;&lt;/rows&gt;。
    /// </summary>
    public class XmlDataReader : BaseReader
    {
        public XmlDataReader(SourceFileInfo info) : base(info) { }

        public override DataReadResult Read(bool onlyReadHeader = false)
        {
            var result = new DataReadResult();

            XDocument document;

            using (var reader = CreateReader())
            {
                document = XDocument.Load(reader);
            }

            var recordElements = FindRecordElements(document);

            var header = new List<string>();

            foreach (var element in recordElements)
            {
                foreach (var child in element.Elements())
                {
                    if (!header.Contains(child.Name.LocalName, System.StringComparer.OrdinalIgnoreCase))
                    {
                        header.Add(child.Name.LocalName);
                    }
                }

                if (header.Count > 0)
                {
                    break;
                }
            }

            result.HeaderColumns = this.info.FirstRowIsColumnName
                ? header.ToArray()
                : header.Select((_, i) => $"column{i + 1}").ToArray();

            if (!onlyReadHeader)
            {
                var dict = new Dictionary<int, Dictionary<int, object>>();

                int index = 0;

                foreach (var element in recordElements)
                {
                    var dictRow = new Dictionary<int, object>();

                    for (int i = 0; i < result.HeaderColumns.Length; i++)
                    {
                        var columnName = this.info.FirstRowIsColumnName ? result.HeaderColumns[i] : header[i];

                        var child = element.Elements().FirstOrDefault(e => e.Name.LocalName == columnName);
                        var value = child?.Value;
                        dictRow.Add(i, string.IsNullOrEmpty(value) ? null : value);
                    }

                    dict.Add(index++, dictRow);
                }

                result.Data = dict;
            }

            return result;
        }

        private static IEnumerable<XElement> FindRecordElements(XDocument document)
        {
            var root = document.Root;

            if (root is null)
            {
                return Enumerable.Empty<XElement>();
            }

            var children = root.Elements().ToList();

            // 常见包装形式：<root><rows><row/></rows></root> —— 仅一个包装子元素时下沉一层。
            if (children.Count == 1 && children[0].HasElements)
            {
                children = children[0].Elements().ToList();
            }

            return children;
        }

        private StreamReader CreateReader()
        {
            var encoding = TextEncoding.Resolve(this.info.EncodingName);

            return encoding is null
                ? new StreamReader(this.info.FilePath)
                : new StreamReader(this.info.FilePath, encoding);
        }
    }
}
