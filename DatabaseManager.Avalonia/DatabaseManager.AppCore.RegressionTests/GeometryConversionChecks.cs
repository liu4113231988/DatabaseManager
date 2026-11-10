using DatabaseInterpreter.Geometry;
using Microsoft.SqlServer.Types;

internal static class GeometryConversionChecks
{
    public static void Run()
    {
        if (!OperatingSystem.IsWindows()) return;

        const string lines = "GEOMETRYCOLLECTION (LINESTRING (0 0, 1 1), LINESTRING (2 2, 3 3))";
        var geometry = SqlGeometryHelper.ToOracleSdoGeometry(SqlGeometry.Parse(lines))!;
        var geography = SqlGeographyHelper.ToOracleSdoGeometry(SqlGeography.Parse(lines))!;
        if (geometry.ElemInfo.Length != 6 || geometry.Ordinates.Length != 8
            || geography.ElemInfo.Length != 6 || geography.Ordinates.Length != 8)
            throw new InvalidOperationException("Geometry collection conversion must preserve every supported member.");

        const string curve = "GEOMETRYCOLLECTION (LINESTRING (0 0, 1 1), CIRCULARSTRING (2 0, 3 1, 4 0))";
        foreach (var convert in new Action[]
        {
            () => SqlGeometryHelper.ToOracleSdoGeometry(SqlGeometry.Parse(curve)),
            () => SqlGeographyHelper.ToOracleSdoGeometry(SqlGeography.Parse(curve)),
        })
        {
            try { convert(); }
            catch (NotSupportedException) { continue; }
            throw new InvalidOperationException("Unsupported geometry members must fail conversion instead of silently losing data.");
        }
        Console.WriteLine("Geometry conversion checks passed: complete collections and unsupported-member rejection.");
    }
}
