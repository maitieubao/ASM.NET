using System.Text.RegularExpressions;
using VibeMusic.Application.Interfaces;

namespace VibeMusic.Infrastructure.External.SemanticKernel;

/// <summary>
/// Extracts structured ACTION commands from the AI response text.
/// Keeps parsing logic isolated and testable.
/// </summary>
public static class ActionExtractor
{
    // Matches: ACTION:play:dQw4w9WgXcQ
    private static readonly Regex PlayRegex =
        new(@"ACTION:play:([a-zA-Z0-9_-]{11})", RegexOptions.Compiled);

    // Matches: ACTION:navigate:playlist:42
    private static readonly Regex PlaylistNavRegex =
        new(@"ACTION:navigate:playlist:(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// Parses the AI response text and returns a SuggestedAction string if found.
    /// Returns null if no action is present.
    /// </summary>
    public static string? Extract(string responseText)
    {
        if (string.IsNullOrEmpty(responseText)) return null;

        var playMatch = PlayRegex.Match(responseText);
        if (playMatch.Success)
            return "play:" + playMatch.Groups[1].Value;

        var navMatch = PlaylistNavRegex.Match(responseText);
        if (navMatch.Success)
            return "navigate:playlist:" + navMatch.Groups[1].Value;

        return null;
    }

    /// <summary>
    /// Strips ACTION commands from the response text for clean display.
    /// </summary>
    public static string StripActions(string responseText)
    {
        if (string.IsNullOrEmpty(responseText)) return responseText;
        var cleaned = PlayRegex.Replace(responseText, "").Trim();
        cleaned = PlaylistNavRegex.Replace(cleaned, "").Trim();
        return cleaned;
    }
}
