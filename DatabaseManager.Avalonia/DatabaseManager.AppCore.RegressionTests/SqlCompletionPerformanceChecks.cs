using System.Diagnostics;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;

internal static class SqlCompletionPerformanceChecks
{
    public static void Run()
    {
        var connection = new DbObjectTreeNode
        {
            Name = "perf", NodeType = DbObjectTreeNodeType.Connection,
            Connection = new() { Name = "perf", DatabaseType = "Postgres", Database = "app" },
        };
        for (int i = 0; i < 5000; i++)
            connection.AddChild(new DbObjectTreeNode
            {
                Name = $"table{i}", Schema = "public", DatabaseName = "app",
                NodeType = DbObjectTreeNodeType.DbObject, DatabaseObjectType = DatabaseObjectType.Table,
            });
        var roots = new[] { connection };
        var query = "SELECT t0. FROM public.table4999 t0 " + string.Join(' ', Enumerable.Range(1, 9)
            .Select(i => $"JOIN public.table{4999 - i} t{i} ON t{i}.id=t0.id"));
        foreach (int statements in new[] { 0, 1000, 10000 })
        {
            var prefix = string.Concat(Enumerable.Repeat("SELECT id, name FROM public.table1 WHERE id > 0;\n", statements));
            var sql = prefix + query;
            int caret = prefix.Length + "SELECT t0.".Length;
            var context = SqlCompletion.Analyze(sql, caret)!;
            Measure($"Analyze {sql.Length:N0} chars", () => SqlCompletion.Analyze(sql, caret));
            Measure("Resolve 10 tables / 5000 objects", () =>
            {
                foreach (var table in context.Tables)
                    if (SqlCompletion.ResolveTable(roots, "perf", "app", table) is null)
                        throw new InvalidOperationException("Benchmark table resolution failed.");
            });
        }
    }

    private static void Measure(string label, Action action)
    {
        action();
        var times = new List<double>();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 15; i++)
        {
            long start = Stopwatch.GetTimestamp();
            action();
            times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        allocated = (GC.GetAllocatedBytesForCurrentThread() - allocated) / times.Count;
        times.Sort();
        Console.WriteLine($"{label}: median {times[times.Count / 2]:F2} ms, {allocated:N0} bytes/op");
    }
}
