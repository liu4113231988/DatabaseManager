namespace DatabaseManager.AppCore.Models;

public sealed record ResultSnapshot(string Title, QueryResult Result, bool IsPinned)
{
    public override string ToString() => Title;
}
