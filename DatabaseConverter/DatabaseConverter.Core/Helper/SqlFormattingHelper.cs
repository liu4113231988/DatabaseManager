using DatabaseInterpreter.Model;
using SQL.Formatter.Core;
using SQL.Formatter.Language;
using System;
using System.Linq;

namespace DatabaseConverter.Core
{
    public static class SqlFormattingHelper
    {
        /// <summary>仅排版；异常或 token 变化时保留原文，不作为语法校验。</summary>
        public static string Format(string sql, DatabaseType databaseType, out bool hasError)
        {
            hasError = false;
            if (string.IsNullOrWhiteSpace(sql)) return sql;
            try
            {
                var config = FormatConfig.Builder().Indent("    ").Case(CaseTypes.NONE).Build();
                AbstractFormatter formatter = databaseType switch
                {
                    DatabaseType.SqlServer => new TSqlFormatter(config),
                    DatabaseType.MySql => new MySqlFormatter(config),
                    DatabaseType.Oracle or DatabaseType.DM => new PlSqlFormatter(config),
                    DatabaseType.Postgres or DatabaseType.KingbaseES => new PostgreSqlFormatter(config),
                    _ => new StandardSqlFormatter(config),
                };
                string formatted = formatter.Format(sql);
                var tokenizer = formatter.Tokenizer();
                var originalTokens = tokenizer.Tokenize(sql).ToList().Select(t => (t.Type, t.Value));
                var formattedTokens = tokenizer.Tokenize(formatted).ToList().Select(t => (t.Type, t.Value));
                if (originalTokens.SequenceEqual(formattedTokens)) return formatted;
            }
            catch (Exception)
            {
                // 不完整或不支持的 SQL 保留原文。
            }
            hasError = true;
            return sql;
        }
    }
}
