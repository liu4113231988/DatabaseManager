using DatabaseManager.AppCore.Models;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DatabaseManager.AppCore.Services;

public static class ExecutionPlanParser
{
    public static IReadOnlyList<ExecutionPlanNode> Parse(QueryResult result)
    {
        var roots = new List<ExecutionPlanNode>();
        if (!result.IsSuccess || result.Rows.Count == 0) return roots;
        string raw = result.Rows[0].FirstOrDefault() ?? "";
        if (raw.TrimStart().StartsWith('['))
        {
            try
            {
                foreach (var item in JArray.Parse(raw))
                    if (item["Plan"] is JObject plan) roots.Add(ParsePostgres(plan));
            }
            catch (Newtonsoft.Json.JsonException) { }
        }
        if (roots.Count == 0)
        {
            var textTree = ParseTextPlan(result);
            if (textTree.Count > 0) roots.AddRange(textTree);
        }
        if (roots.Count == 0)
        {
            int Find(string name) => result.Columns.ToList().FindIndex(c => c.Equals(name, StringComparison.OrdinalIgnoreCase));
            string Cell(IReadOnlyList<string> row, string name) { int i = Find(name); return i >= 0 && i < row.Count ? row[i] : ""; }
            var nodes = new Dictionary<string, ExecutionPlanNode>();
            var parents = new List<(ExecutionPlanNode Node, string Parent)>();
            foreach (var row in result.Rows)
            {
                string operation = Cell(row, "PhysicalOp");
                if (operation.Length == 0) operation = Cell(row, "detail");
                if (operation.Length == 0) operation = Cell(row, "type");
                if (operation.Length == 0) operation = string.Join(" | ", row);
                var node = new ExecutionPlanNode
                {
                    Operation = operation, ObjectName = Cell(row, "table"),
                    Cost = Number(Cell(row, "TotalSubtreeCost")), Details = string.Join("\n", result.Columns.Zip(row, (c,v) => $"{c}: {v}")),
                };
                string id = Cell(row, "NodeId");
                if (id.Length == 0) id = Cell(row, "id");
                if (id.Length > 0) nodes.TryAdd(id, node);
                parents.Add((node, Cell(row, "parent")));
            }
            foreach (var (node, parent) in parents)
            {
                if (nodes.TryGetValue(parent, out var p) && p != node && !Descendants(node).Contains(p)) p.Children.Add(node);
                else roots.Add(node);
            }
        }
        var all = roots.SelectMany(Descendants).ToList();
        double maxCost = all.Max(n => n.Cost ?? 0);
        foreach (var node in all) node.IsHotspot = maxCost > 0 && node.Cost >= maxCost * .5;
        return roots;
    }

    private static List<ExecutionPlanNode> ParseTextPlan(QueryResult result)
    {
        var roots = new List<ExecutionPlanNode>();
        if (result.Columns.Count > 1) return roots;
        var stack = new Stack<(int Indent, ExecutionPlanNode Node)>();
        foreach (string line in result.Rows.SelectMany(r => r).SelectMany(s => s.Split('\n')))
        {
            string operation, objectName = "";
            int indent;
            double? cost = null, time = null;
            var oracle = Regex.Match(line, @"^\s*\|\s*\*?\s*\d+\s*\|(?<operation>[^|]+)\|(?<object>[^|]*)\|");
            if (oracle.Success)
            {
                var op = oracle.Groups["operation"].Value;
                indent = op.Length - op.TrimStart().Length;
                operation = op.Trim(); objectName = oracle.Groups["object"].Value.Trim();
                var cells = line.Split('|');
                if (cells.Length > 6) cost = Number(Regex.Match(cells[6], @"\d+(\.\d+)?").Value);
            }
            else
            {
                int arrow = line.IndexOf("->", StringComparison.Ordinal);
                if (arrow < 0 && !line.Contains("(cost=", StringComparison.Ordinal)) continue;
                indent = arrow >= 0 ? arrow : line.Length - line.TrimStart().Length;
                operation = (arrow >= 0 ? line[(arrow + 2)..] : line).Trim();
                var costMatch = Regex.Match(operation, @"cost=\d+(?:\.\d+)?(?:\.\.(?<total>\d+(?:\.\d+)?))?");
                cost = Number(costMatch.Groups["total"].Value);
                time = Number(Regex.Match(operation, @"actual time=\d+(?:\.\d+)?\.\.(?<total>\d+(?:\.\d+)?)").Groups["total"].Value);
                objectName = Regex.Match(operation, @"\bon\s+(?<name>[^\s(]+)").Groups["name"].Value;
                int metrics = operation.IndexOf(" (", StringComparison.Ordinal);
                if (metrics > 0) operation = operation[..metrics];
            }
            var node = new ExecutionPlanNode { Operation = operation, ObjectName = objectName, Cost = cost, Milliseconds = time, Details = line };
            while (stack.Count > 0 && stack.Peek().Indent >= indent) stack.Pop();
            if (stack.Count == 0) roots.Add(node); else stack.Peek().Node.Children.Add(node);
            stack.Push((indent, node));
        }
        return roots;
    }

    private static ExecutionPlanNode ParsePostgres(JObject plan)
    {
        var node = new ExecutionPlanNode
        {
            Operation = (string?)plan["Node Type"] ?? "Plan", ObjectName = (string?)plan["Relation Name"] ?? "",
            Cost = (double?)plan["Total Cost"], Milliseconds = (double?)plan["Actual Total Time"], Details = plan.ToString(),
        };
        if (plan["Plans"] is JArray children)
            foreach (var child in children.OfType<JObject>()) node.Children.Add(ParsePostgres(child));
        return node;
    }
    private static double? Number(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
    private static IEnumerable<ExecutionPlanNode> Descendants(ExecutionPlanNode node)
    {
        yield return node;
        foreach (var child in node.Children.SelectMany(Descendants)) yield return child;
    }
}
