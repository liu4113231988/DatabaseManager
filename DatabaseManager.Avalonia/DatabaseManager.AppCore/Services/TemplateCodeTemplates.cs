using DatabaseManager.AppCore.Templating;
using DatabaseManager.Core;

namespace DatabaseManager.AppCore.Services;

/// <summary>把 T3 代码模板（含用户覆盖）装配为旧版 CodeGenerator 可消费的模板集合。</summary>
public static class TemplateCodeTemplates
{
    public static CodeTemplateSet Create(ITemplateStore? store)
    {
        if (store is null) return CodeTemplateSet.Default;

        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in store.GetAll("code"))
        {
            contents[template.Id] = template.Content;
        }
        return CodeTemplateSet.FromContents(contents);
    }
}
