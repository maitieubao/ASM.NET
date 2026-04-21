using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace VibeMusic.Infrastructure.External.SemanticKernel;

/// <summary>
/// Handles retry logic for transient AI/network errors.
/// Respects Groq rate-limit headers and uses exponential backoff for other failures.
/// </summary>
public static class RetryHandler
{
    private static readonly Regex RetryAfterRegex =
        new(@"Please try again in\s+([0-9]+(?:\.[0-9]+)?)s", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Executes the given async operation with retry logic.
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries,
        ILogger logger,
        string operationName = "AI call")
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastException = ex;
                var delay = GetRetryDelay(ex, attempt);

                logger.LogWarning(
                    ex,
                    "[RetryHandler] {Operation} failed (attempt {Attempt}/{Max}). Retrying in {DelayMs}ms.",
                    operationName, attempt, maxRetries, (int)delay.TotalMilliseconds);

                await Task.Delay(delay);
            }
        }

        // Final attempt — let exception propagate
        return await operation();
    }

    /// <summary>
    /// Builds a user-friendly error message from an exception.
    /// </summary>
    public static string BuildUserFriendlyError(Exception ex)
    {
        var text = ex.ToString();

        if (text.Contains("rate_limit_exceeded", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("HTTP 429", StringComparison.OrdinalIgnoreCase))
        {
            return "AI đang bận do quá nhiều yêu cầu cùng lúc. Bạn vui lòng thử lại sau 15-20 giây nhé.";
        }

        return "Rất tiếc, máy chủ AI đang bận hoặc gặp sự cố kết nối. Bạn vui lòng thử lại sau giây lát nhé!";
    }

    private static TimeSpan GetRetryDelay(Exception ex, int attempt)
    {
        var text = ex.ToString();
        var match = RetryAfterRegex.Match(text);

        if (match.Success &&
            double.TryParse(
                match.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var seconds) &&
            seconds > 0)
        {
            // Add small buffer for clock/network jitter
            return TimeSpan.FromSeconds(seconds + 0.5);
        }

        // Exponential backoff: 2s, 4s, 8s... capped at 10s
        return TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 10));
    }
}
