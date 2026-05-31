namespace PromptTasks.Infrastructure.AgentUsage;

internal static class AgentUsageText
{
    public static string? Sanitize(string? detail, string? secret = null)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        var sanitized = detail.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(secret))
        {
            sanitized = sanitized.Replace(secret, "[redacted]", StringComparison.Ordinal);
        }

        const int maxLength = 240;
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }

    public static double ClampPercent(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Clamp(value, 0, 100);
    }
}
