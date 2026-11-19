using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DatabaseManager.Core
{
    /// <summary>
    /// T3 代码生成模板集合：把 C# / Java 的骨架与循环片段外置，默认内容与历史输出保持一致。
    /// </summary>
    public sealed class CodeTemplateSet
    {
        private static readonly Regex PlaceholderRegex = new(@"\{([a-z]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static CodeTemplateSet Default { get; } = new();

        public string CSharpComment { get; set; } = "{indent}/// <summary>\n{indent}/// {comment}\n{indent}/// </summary>\n";
        public string JavaClassComment { get; set; } = "{indent}/*\n{indent} *{comment}\n{indent} */\n";
        public string JavaFieldComment { get; set; } = "{indent}//{comment}\n";
        public string CSharpProperty { get; set; } = "{indent}public {type}{nullable} {name} {get; set;}\n";
        public string JavaField { get; set; } = "{indent}private {type} {field};\n";
        public string JavaMethod { get; set; } =
            "{indent}public {type} get{property}() {\n{indent}\treturn this.{field};\n{indent}}\n\n" +
            "{indent}public void set{property}({type} {field}) {\n{indent}\tthis.{field} = {field};\n{indent}}\n";
        public string CSharpNamespace { get; set; } = "namespace {namespace}\n{\n";
        public string CSharpNamespaceEnd { get; set; } = "}\n";
        public string CSharpClass { get; set; } = "{indent}public class {class}\n{indent}{\n";
        public string CSharpClassEnd { get; set; } = "{indent}}\n";
        public string CSharpUsing { get; set; } = "using {import};\n";
        public string JavaPackage { get; set; } = "package {namespace};\n\n";
        public string JavaImport { get; set; } = "import {import};\n";
        public string JavaClass { get; set; } = "public class {class} {\n";
        public string JavaClassEnd { get; set; } = "}\n";

        /// <summary>按 {name} 语法展开；模板中的换行统一为当前平台换行符。</summary>
        public static string Expand(string template, IReadOnlyDictionary<string, string> values)
        {
            string normalized = (template ?? string.Empty).ReplaceLineEndings(Environment.NewLine);
            return PlaceholderRegex.Replace(normalized, match =>
                values.TryGetValue(match.Groups[1].Value, out string value)
                    ? value
                    : throw new ArgumentException("未知代码模板变量：" + match.Value));
        }

        /// <summary>从模板仓库的 id → Content 字典构建；缺失项保留默认值。</summary>
        public static CodeTemplateSet FromContents(IReadOnlyDictionary<string, string> contents)
        {
            var set = new CodeTemplateSet();
            set.CSharpComment = Pick(contents, "code-csharp-comment", set.CSharpComment);
            set.JavaClassComment = Pick(contents, "code-java-class-comment", set.JavaClassComment);
            set.JavaFieldComment = Pick(contents, "code-java-field-comment", set.JavaFieldComment);
            set.CSharpProperty = Pick(contents, "code-csharp-property", set.CSharpProperty);
            set.JavaField = Pick(contents, "code-java-field", set.JavaField);
            set.JavaMethod = Pick(contents, "code-java-method", set.JavaMethod);
            set.CSharpNamespace = Pick(contents, "code-csharp-namespace", set.CSharpNamespace);
            set.CSharpNamespaceEnd = Pick(contents, "code-csharp-namespace-end", set.CSharpNamespaceEnd);
            set.CSharpClass = Pick(contents, "code-csharp-class", set.CSharpClass);
            set.CSharpClassEnd = Pick(contents, "code-csharp-class-end", set.CSharpClassEnd);
            set.CSharpUsing = Pick(contents, "code-csharp-using", set.CSharpUsing);
            set.JavaPackage = Pick(contents, "code-java-package", set.JavaPackage);
            set.JavaImport = Pick(contents, "code-java-import", set.JavaImport);
            set.JavaClass = Pick(contents, "code-java-class", set.JavaClass);
            set.JavaClassEnd = Pick(contents, "code-java-class-end", set.JavaClassEnd);
            return set;
        }

        private static string Pick(IReadOnlyDictionary<string, string> contents, string id, string fallback)
            => contents.TryGetValue(id, out string content) && !string.IsNullOrWhiteSpace(content) ? content : fallback;
    }
}
