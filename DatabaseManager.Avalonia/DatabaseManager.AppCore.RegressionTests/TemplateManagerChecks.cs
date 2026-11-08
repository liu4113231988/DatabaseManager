using DatabaseManager.AppCore.Templating;
using DatabaseManager.AppCore.ViewModels;
using DatabaseManager.AppCore.Services;

internal static class TemplateManagerChecks
{
    public static void Run()
    {
        var store = new Store();
        var vm = new TemplateManagerViewModel(store, new TemplateEngine());
        vm.EditingContent = "SELECT '{table}'";
        vm.PreviewCommand.Execute(null);
        Check(vm.PreviewText == "SELECT 'sample'", "Preview must expand example variables without executing SQL.");
        vm.SaveCommand.Execute(null);
        Check(store.User?.Content == vm.EditingContent && store.Builtin.Content == "SELECT {table}", "Save must override without mutating the builtin.");
        var libraryStore = new Library();
        var library = new ScriptLibraryViewModel(libraryStore, store);
        library.SelectedScript = library.BuiltInScripts.Single();
        string? inserted = null;
        library.InsertToEditorRequested = text => inserted = text;
        library.InsertToEditorCommand.Execute(null);
        Check(inserted == store.User!.Content && libraryStore.Writes == 0, "The script library must consume overrides without copying builtin templates into user scripts.");
        vm.EditingContent = "SELECT {unknown}";
        vm.SaveCommand.Execute(null);
        Check(store.User?.Content == "SELECT '{table}'" && vm.StatusMessage.Contains("保存失败"), "Invalid templates must not overwrite saved content.");
        vm.RestoreDefaultCommand.Execute(null);
        Check(store.User is null && vm.EditingContent == "SELECT {table}", "Restore must remove only the user override.");
        store.FailSave = true;
        vm.SaveCommand.Execute(null);
        Check(vm.StatusMessage.Contains("保存失败"), "Storage failure must remain visible.");
        foreach (var unsafeId in new[] { "../outside", "..", "a/b", "a\\b", "C:escape" })
        {
            try
            {
                new DefaultTemplateStore().Save(new() { Id = unsafeId });
                throw new InvalidOperationException("Unsafe template paths must be rejected.");
            }
            catch (ArgumentException) { }
        }
        Console.WriteLine("Template manager checks passed.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Library : IScriptLibraryService
    {
        public int Writes;
        public IReadOnlyList<ScriptLibraryItem> GetAll() => [];
        public void Save(ScriptLibraryItem item) => Writes++;
        public bool Delete(string id) => false;
        public IReadOnlyList<string> GetRecentFiles() => [];
        public void AddRecentFile(string path) { }
    }
    private sealed class Store : ITemplateStore
    {
        public TemplateManifest Builtin = new() { Id = "sample", Name = "Sample", Content = "SELECT {table}", Variables = [new() { Name = "table", Sample = "sample" }] };
        public TemplateManifest? User;
        public bool FailSave;
        public IReadOnlyList<TemplateManifest> GetAll(string? kind = null) => [User ?? Builtin];
        public TemplateManifest? Get(string kind, string id) => User ?? Builtin;
        public IReadOnlyList<TemplateManifest> GetBuiltin(string? kind = null) => [Builtin];
        public void Save(TemplateManifest template)
        {
            if (FailSave) throw new IOException("fixture write failure");
            template.Source = "user";
            User = template;
        }
        public bool Delete(string kind, string id) { User = null; return true; }
    }
}
