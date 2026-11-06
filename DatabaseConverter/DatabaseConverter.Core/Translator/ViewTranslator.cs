using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using TSQL.Tokens;

namespace DatabaseConverter.Core
{
    public class ViewTranslator: DbObjectTokenTranslator
    {
        private List<View> views;     
       
        private string targetSchemaName;              

        public ViewTranslator(DbInterpreter sourceDbInterpreter, DbInterpreter targetDbInterpreter, List<View> views, string targetSchemaName = null): base(sourceDbInterpreter, targetDbInterpreter)
        {
            this.views = views;           
            this.targetSchemaName = targetSchemaName;
        }

        public override void Translate()
        {
            if (sourceDbInterpreter.DatabaseType == targetDbInterpreter.DatabaseType)
            {
                return;
            }

            if (this.hasError)
            {
                return;
            }

            this.LoadMappings();

            if (string.IsNullOrEmpty(targetSchemaName))
            {
                if (targetDbInterpreter is SqlServerInterpreter)
                {
                    targetSchemaName = "dbo";
                }
                else
                {
                    targetSchemaName = targetDbInterpreter.DefaultSchema;
                }
            }

            foreach (View view in views)
            {
                try
                {                    
                    string viewNameWithQuotation = $"{targetDbInterpreter.QuotationLeftChar}{view.Name}{targetDbInterpreter.QuotationRightChar}";

                    string definition = view.Definition;
                    string columnList = string.Empty;

                    // 先按 token 去掉旧视图头，避免名称、注释及字面量中的 AS 被当作分隔符。
                    var tokens = this.GetTokens(definition).Where(t => t.Type is not
                        (TSQLTokenType.Whitespace or TSQLTokenType.SingleLineComment or TSQLTokenType.MultilineComment)).ToList();
                    if (tokens.FirstOrDefault()?.Text.Equals("CREATE", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        int viewIndex = tokens.FindIndex(t => t.Type == TSQLTokenType.Keyword && t.Text.Equals("VIEW", StringComparison.OrdinalIgnoreCase));
                        TSQLToken asToken = null;
                        int depth = 0, columnStart = -1;
                        foreach (var token in tokens.Skip(viewIndex + 1))
                        {
                            if (token.Type == TSQLTokenType.Character && token.Text == "(")
                            {
                                if (depth == 0) columnStart = token.BeginPosition;
                                depth++;
                            }
                            if (token.Type == TSQLTokenType.Character && token.Text == ")")
                            {
                                depth--;
                                if (depth == 0 && columnStart >= 0)
                                    columnList = this.ParseDefinition(definition.Substring(columnStart, token.EndPosition - columnStart + 1));
                            }
                            if (depth == 0 && token.Type == TSQLTokenType.Keyword && token.Text.Equals("AS", StringComparison.OrdinalIgnoreCase))
                            {
                                asToken = token;
                                break;
                            }
                        }
                        if (viewIndex < 0 || asToken is null) throw new FormatException("无法识别视图定义的 AS 分隔符。");
                        definition = definition.Substring(asToken.EndPosition + 1);
                    }

                    definition = this.ParseDefinition(definition);

                    string createClause = this.targetDbInterpreter.DatabaseType == DatabaseType.Oracle ? "CREATE OR REPLACE" : "CREATE";

                    string createAsClause = $"{createClause} VIEW {(string.IsNullOrEmpty(targetSchemaName)? "": targetSchemaName + "." )}{viewNameWithQuotation}{columnList} AS ";

                    definition = createAsClause + Environment.NewLine + definition;

                    view.Definition = definition;

                    if (this.Option.CollectTranslateResultAfterTranslated)
                    {
                        this.TranslateResults.Add(new TranslateResult() { DbObjectType = DatabaseObjectType.View, DbObjectName = view.Name, Data = view.Definition });
                    }
                }
                catch (Exception ex)
                {
                    var vce = new ViewConvertException(ex)
                    {
                        SourceServer = sourceDbInterpreter.ConnectionInfo.Server,
                        SourceDatabase = sourceDbInterpreter.ConnectionInfo.Database,
                        SourceObject = view.Name,
                        TargetServer = targetDbInterpreter.ConnectionInfo.Server,
                        TargetDatabase = targetDbInterpreter.ConnectionInfo.Database,
                        TargetObject = view.Name
                    };

                    if (!this.ContinueWhenErrorOccurs)
                    {
                        throw vce;
                    }
                    else
                    {
                        this.FeedbackError(ExceptionHelper.GetExceptionDetails(ex), this.ContinueWhenErrorOccurs);
                    }                   
                }
            }           
        }

        public override string ParseDefinition(string definition)
        {
            definition = base.ParseDefinition(definition);

            // 只排版，不再根据换行猜测 JOIN 条件，避免改写字符串、注释及嵌套查询。
            if (this.sourceDbType == DatabaseType.MySql)
            {
                string formattedDefinition = this.FormatSql(definition, out bool hasFormatError);
                if (!hasFormatError) definition = formattedDefinition;
            } 

            return definition.Trim();
        }             
    }
}
