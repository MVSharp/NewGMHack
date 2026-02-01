using Spectre.Console;

namespace Updater;

/// <summary>
/// Formats and displays release notes/changelog with Spectre.Console markup
/// </summary>
public static class ChangelogFormatter
{
    /// <summary>
    /// Display brief changelog (top 5 items) in a panel during update
    /// </summary>
    public static void DisplayBriefChangelog(string version, string markdown)
    {
        var lines = markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var changes = new List<string>();

        // Extract first 5 bullet points or list items
        int count = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("-") || trimmed.StartsWith("*") || trimmed.StartsWith("•"))
            {
                var formatted = FormatMarkdownLine(trimmed);
                changes.Add(formatted);
                count++;

                if (count >= 5)
                {
                    changes.Add("[dim]...and more[/]");
                    break;
                }
            }
        }

        if (changes.Count == 0)
        {
            // No bullet points found, show first paragraph
            var firstPara = string.Join(" ", lines.Take(3)).Trim();
            if (!string.IsNullOrEmpty(firstPara))
            {
                changes.Add(FormatMarkdownLine(firstPara));
            }
        }

        // Display in panel
        var panel = new Panel(
            new Rows(
                changes.Select(c => new Markup(c)).ToArray()
            ))
        {
            Header = new PanelHeader($"[bold cyan]What's New in {version}[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("cyan"),
            Padding = new Padding(1, 0, 1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Display full changelog with all release notes
    /// </summary>
    public static void DisplayFullChangelog(string version, string markdown)
    {
        AnsiConsole.WriteLine();
        var rule = new Rule($"[bold green]Release Notes for {version}[/]");
        rule.Justification = Justify.Center;
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        var formatted = FormatMarkdown(markdown);
        var panel = new Panel(new Markup(formatted))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("green"),
            Padding = new Padding(1, 0, 1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Convert markdown to Spectre.Console markup
    /// </summary>
    private static string FormatMarkdown(string markdown)
    {
        var lines = markdown.Split('\n');
        var result = new List<string>();
        var inCodeBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Code blocks
            if (trimmed.StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                result.Add("[dim]────────────────────────────────────────[/]");
                continue;
            }

            if (inCodeBlock)
            {
                result.Add($"[dim]{EscapeMarkup(trimmed)}[/]");
                continue;
            }

            // Headers
            if (trimmed.StartsWith("###"))
            {
                result.Add($"[bold yellow]{FormatMarkdownLine(trimmed.Substring(3).Trim())}[/]");
                continue;
            }

            if (trimmed.StartsWith("##"))
            {
                result.Add($"[bold cyan]{FormatMarkdownLine(trimmed.Substring(2).Trim())}[/]");
                continue;
            }

            if (trimmed.StartsWith("#"))
            {
                result.Add($"[bold green]{FormatMarkdownLine(trimmed.Substring(1).Trim())}[/]");
                continue;
            }

            // List items
            if (trimmed.StartsWith("-") || trimmed.StartsWith("*") || trimmed.StartsWith("•"))
            {
                result.Add($"[white]•[/] {FormatMarkdownLine(trimmed.Substring(1).Trim())}");
                continue;
            }

            // Regular text
            if (!string.IsNullOrEmpty(trimmed))
            {
                result.Add(FormatMarkdownLine(trimmed));
            }
        }

        return string.Join("\n", result);
    }

    /// <summary>
    /// Format single line of markdown to Spectre.Console markup
    /// </summary>
    private static string FormatMarkdownLine(string line)
    {
        // Escape existing markup first
        var result = EscapeMarkup(line);

        // Bold: **text** or __text__
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\*\*(.+?)\*\*|__(.+?)__",
            "[bold]$1$2[/]");

        // Italic: *text* or _text_
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)|(?<!_)_(?!_)(.+?)(?<!_)_(?!_)",
            "[italic]$1$2[/]");

        // Code: `text`
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"`(.+?)`",
            "[dim on blue]$1[/]");

        // Links: [text](url)
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\[(.+?)\]\((.+?)\)",
            "[link]$1[/][dim]($2)[/]");

        return result;
    }

    /// <summary>
    /// Escape special Spectre.Console markup characters
    /// </summary>
    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[")
            .Replace("]", "]]");
    }
}
