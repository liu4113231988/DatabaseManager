using DatabaseManager.AppCore.Services;
using Newtonsoft.Json;

namespace DatabaseManager.AppCore.Templating;

/// <summary>
/// 模板目录：加载/保存外部化模板。
/// 目录约定（P3 T1）：用户模板 <c>Profiles/Templates/&lt;kind&gt;/&lt;id&gt;.tpl</c>（JSON 序列化的 TemplateManifest）；
/// 内置模板随程序发布（当前由 SqlSnippets 桥接为只读内置清单），用户目录同 Kind+Id 同名覆盖，不做就地改写内置文件。
/// </summary>
public interface ITemplateStore
{
    /// <summary>列出模板（内置 + 用户，用户同名覆盖内置；按名称排序）。</summary>
    IReadOnlyList<TemplateManifest> GetAll(string? kind = null);

    /// <summary>取单个模板（用户覆盖优先；不存在返回 null）。</summary>
    TemplateManifest? Get(string kind, string id);

    /// <summary>保存（新增或更新）用户模板到 Profiles/Templates。</summary>
    void Save(TemplateManifest manifest);

    /// <summary>删除用户模板；内置模板不可删除（返回 false）。</summary>
    bool Delete(string kind, string id);

    /// <summary>列出内置模板（只读）。</summary>
    IReadOnlyList<TemplateManifest> GetBuiltin(string? kind = null);
}

public class DefaultTemplateStore : ITemplateStore
{
    private static readonly string RootFolder =
        Path.Combine(AppContext.BaseDirectory, "Profiles", "Templates");

    private static readonly object FileLock = new();

    public IReadOnlyList<TemplateManifest> GetAll(string? kind = null)
    {
        var result = new List<TemplateManifest>();
        var overriddenIds = new HashSet<(string, string)>();

        // 用户模板优先（同名覆盖内置）。
        foreach (var user in LoadUserTemplates(kind))
        {
            result.Add(user);
            overriddenIds.Add((user.Kind, user.Id));
        }

        // 内置项若存在用户覆盖则不再重复列出。
        foreach (var builtin in GetBuiltin(kind))
        {
            if (!overriddenIds.Contains((builtin.Kind, builtin.Id)))
            {
                result.Add(builtin);
            }
        }

        return result.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public TemplateManifest? Get(string kind, string id)
    {
        var user = LoadUserTemplates(kind).FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
        if (user is not null)
        {
            return user;
        }

        return GetBuiltin(kind).FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public void Save(TemplateManifest manifest)
    {
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Kind) || string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new ArgumentException("模板 Kind 与 Id 不能为空。");
        }

        var folder = Path.Combine(RootFolder, manifest.Kind);
        lock (FileLock)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"{manifest.Id}.tpl");
            manifest.Source = "user";
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(manifest, Formatting.Indented));
            File.Move(tmp, path, true); // 原子写
        }
    }

    public bool Delete(string kind, string id)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        // 内置模板不可删除（用户覆盖文件删除后自动回退内置）。
        var path = Path.Combine(RootFolder, kind, $"{id}.tpl");
        lock (FileLock)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                File.Delete(path);
                return true;
            }
            catch
            {
                // 删除失败（占用/权限）不影响主流程。
                return false;
            }
        }
    }

    public IReadOnlyList<TemplateManifest> GetBuiltin(string? kind = null)
    {
        // 内置清单桥接：SqlSnippets（纯文本、无逻辑）作为首批内置模板（对应 P3 T3 第 1 批）。
        IEnumerable<TemplateManifest> builtins = SqlSnippets.BuiltIn.Select(item => new TemplateManifest
        {
            Id = item.Id,
            Name = item.Name,
            Kind = "sql-snippet",
            Version = TemplateContract.CurrentVersion,
            Engine = "simple",
            Language = "sql",
            AppliesToDialects = new List<string>(),
            Variables = new List<TemplateVariable>(),
            Content = item.SqlText,
            Source = "builtin",
        });

        if (!string.IsNullOrEmpty(kind))
        {
            builtins = builtins.Where(t => string.Equals(t.Kind, kind, StringComparison.OrdinalIgnoreCase));
        }

        return builtins.ToList();
    }

    private static List<TemplateManifest> LoadUserTemplates(string? kind = null)
    {
        var result = new List<TemplateManifest>();
        var root = RootFolder;
        if (!Directory.Exists(root))
        {
            return result;
        }

        var directories = string.IsNullOrEmpty(kind)
            ? Directory.EnumerateDirectories(root)
            : [Path.Combine(root, kind)];

        foreach (var folder in directories)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "*.tpl"))
            {
                try
                {
                    var manifest = JsonConvert.DeserializeObject<TemplateManifest>(File.ReadAllText(file));
                    if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Kind))
                    {
                        continue; // 契约不完整：跳过，不中断整目录加载。
                    }

                    manifest.Source = "user";
                    result.Add(manifest);
                }
                catch
                {
                    // 单个模板文件损坏：跳过（可由用户在模板管理中删除重建），不静默回退到内置同名项。
                }
            }
        }

        return result;
    }
}
