using System;
using System.Text;

namespace DatabaseManager.FileUtility
{
    /// <summary>文本编码解析（支持 UTF-8 / UTF-8 BOM / GBK / GB18030 等名称，空或 auto 表示自动探测）。</summary>
    public static class TextEncoding
    {
        /// <summary>常用编码选项（供 UI 下拉展示）。</summary>
        public static readonly string[] CommonNames =
        {
            "auto（自动/BOM 探测）",
            "utf-8",
            "utf-8-sig",
            "gbk",
            "gb18030",
            "big5",
            "latin1",
        };

        public static bool IsAuto(string? encodingName)
            => string.IsNullOrWhiteSpace(encodingName)
               || encodingName.Trim().StartsWith("auto", StringComparison.OrdinalIgnoreCase);

        /// <summary>把编码名称解析为 <see cref="Encoding"/>；auto/空返回 null（调用方使用默认行为）。</summary>
        public static Encoding? Resolve(string? encodingName)
        {
            if (IsAuto(encodingName))
            {
                return null;
            }

            var name = encodingName!.Trim();

            if (name.Equals("utf-8-sig", StringComparison.OrdinalIgnoreCase))
            {
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            }

            try
            {
                return Encoding.GetEncoding(name);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
