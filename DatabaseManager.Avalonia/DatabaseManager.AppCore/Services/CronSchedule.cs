namespace DatabaseManager.AppCore.Services;

/// <summary>应用运行期间使用的五段 Cron（分 时 日 月 周）计算器。</summary>
public static class CronSchedule
{
    public static DateTime GetNextOccurrence(string expression, DateTime from)
    {
        if (!TryGetNextOccurrence(expression, from, out var next, out var error))
            throw new ArgumentException(error, nameof(expression));
        return next;
    }

    public static bool TryGetNextOccurrence(string expression, DateTime from, out DateTime next, out string? error)
    {
        next = default;
        error = null;
        var fields = (expression ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != 5)
        {
            error = "Cron 必须由 5 段组成：分 时 日 月 周。";
            return false;
        }

        if (!TryParseField(fields[0], 0, 59, out var minute, out error)
            || !TryParseField(fields[1], 0, 23, out var hour, out error)
            || !TryParseField(fields[2], 1, 31, out var day, out error)
            || !TryParseField(fields[3], 1, 12, out var month, out error)
            || !TryParseField(fields[4], 0, 7, out var week, out error))
            return false;

        var candidate = new DateTime(from.Year, from.Month, from.Day, from.Hour, from.Minute, 0).AddMinutes(1);
        var limit = candidate.AddYears(2);
        while (candidate < limit)
        {
            int cronDayOfWeek = (int)candidate.DayOfWeek;
            bool matchesDayOfWeek = week.Contains(cronDayOfWeek) || (cronDayOfWeek == 0 && week.Contains(7));
            bool matchesDay = day.Contains(candidate.Day);
            bool dayMatches = (day.IsWildcard && week.IsWildcard)
                || (day.IsWildcard ? matchesDayOfWeek : week.IsWildcard ? matchesDay : matchesDay || matchesDayOfWeek);
            if (minute.Contains(candidate.Minute) && hour.Contains(candidate.Hour)
                && month.Contains(candidate.Month) && dayMatches)
            {
                next = candidate;
                return true;
            }
            candidate = candidate.AddMinutes(1);
        }

        error = "无法在未来两年内计算下次执行时间。";
        return false;
    }

    private static bool TryParseField(string field, int min, int max, out CronField result, out string? error)
    {
        result = new CronField(false, new HashSet<int>());
        error = null;
        if (string.IsNullOrWhiteSpace(field))
        {
            error = "Cron 字段不能为空。";
            return false;
        }

        bool wildcard = field == "*";
        foreach (var segment in field.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var stepParts = segment.Split('/', StringSplitOptions.TrimEntries);
            if (stepParts.Length > 2 || (stepParts.Length == 2 && (!int.TryParse(stepParts[1], out var stepValue) || stepValue < 1)))
            {
                error = $"无效的 Cron 字段：{field}";
                return false;
            }
            int step = stepParts.Length == 2 ? int.Parse(stepParts[1]) : 1;
            var range = stepParts[0] == "*" ? (min, max) : ParseRange(stepParts[0], min, max);
            if (range is null)
            {
                error = $"无效的 Cron 字段：{field}";
                return false;
            }
            for (int value = range.Value.Item1; value <= range.Value.Item2; value += step)
                result.Values.Add(value);
        }

        result = result with { IsWildcard = wildcard };
        return result.Values.Count > 0;
    }

    private static (int, int)? ParseRange(string value, int min, int max)
    {
        if (int.TryParse(value, out var single) && single >= min && single <= max)
            return (single, single);
        var parts = value.Split('-', StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end)
            && start >= min && end <= max && start <= end ? (start, end) : null;
    }

    private sealed record CronField(bool IsWildcard, HashSet<int> Values)
    {
        public bool Contains(int value) => Values.Contains(value);
    }
}
