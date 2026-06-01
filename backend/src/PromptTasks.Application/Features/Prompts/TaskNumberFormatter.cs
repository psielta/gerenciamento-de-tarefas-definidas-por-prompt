using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PromptTasks.Application.Features.Prompts;

public static partial class TaskNumberFormatter
{
    public const int MaxPatternLength = 100;
    public const int MaxTaskNumberLength = 64;
    private const string DefaultDateFormat = "ddMMyy";

    private static readonly HashSet<char> AllowedLiteralChars = new("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-");
    private static readonly HashSet<string> DateTokens = new(StringComparer.Ordinal) { "dd", "MM", "yy", "yyyy" };

    public static string Format(string pattern, int sequence, DateOnly date)
    {
        var result = Render(pattern, sequence, date);
        if (!TaskNumberRegex().IsMatch(result))
        {
            throw new InvalidOperationException("The generated task number does not match the allowed format.");
        }

        return result;
    }

    public static IReadOnlyList<string> Validate(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return Array.Empty<string>();
        }

        var errors = new List<string>();
        if (pattern.Length > MaxPatternLength)
        {
            errors.Add($"Pattern must be at most {MaxPatternLength} characters.");
        }

        var tokens = Parse(pattern, errors, validateLiterals: true);
        if (!tokens.Any(token => token.Kind == TokenKind.Sequence))
        {
            errors.Add("Pattern must include {N}.");
        }

        if (!tokens.Any(token => token.Kind == TokenKind.Date))
        {
            errors.Add("Pattern must include {Date}.");
        }

        if (errors.Count == 0)
        {
            var rendered = Render(pattern, sequence: 1, new DateOnly(2026, 5, 28));
            if (!TaskNumberRegex().IsMatch(rendered))
            {
                errors.Add("Generated task number must contain 1 to 64 URL-safe characters.");
            }
        }

        return errors;
    }

    private static string Render(string pattern, int sequence, DateOnly date)
    {
        var errors = new List<string>();
        var tokens = Parse(pattern, errors, validateLiterals: false);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(errors[0]);
        }

        var builder = new StringBuilder();
        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case TokenKind.Literal:
                    builder.Append(token.Value);
                    break;
                case TokenKind.Sequence:
                    builder.Append(FormatSequence(sequence, token.Value));
                    break;
                case TokenKind.Date:
                    builder.Append(FormatDate(date, token.Value));
                    break;
            }
        }

        return builder.ToString();
    }

    private static List<Token> Parse(string pattern, List<string> errors, bool validateLiterals)
    {
        var tokens = new List<Token>();
        var index = 0;
        while (index < pattern.Length)
        {
            var open = pattern.IndexOf('{', index);
            if (open < 0)
            {
                AddLiteral(tokens, errors, pattern[index..], validateLiterals);
                break;
            }

            if (open > index)
            {
                AddLiteral(tokens, errors, pattern[index..open], validateLiterals);
            }

            var close = pattern.IndexOf('}', open + 1);
            if (close < 0)
            {
                errors.Add("Pattern contains an unclosed token.");
                break;
            }

            var rawToken = pattern[(open + 1)..close];
            AddToken(tokens, errors, rawToken);
            index = close + 1;
        }

        return tokens;
    }

    private static void AddLiteral(List<Token> tokens, List<string> errors, string literal, bool validate)
    {
        if (literal.Length == 0)
        {
            return;
        }

        if (validate && literal.Any(character => !AllowedLiteralChars.Contains(character)))
        {
            errors.Add("Literal text may only contain letters, numbers, underscore and hyphen.");
        }

        tokens.Add(new Token(TokenKind.Literal, literal));
    }

    private static void AddToken(List<Token> tokens, List<string> errors, string rawToken)
    {
        if (rawToken == "N")
        {
            tokens.Add(new Token(TokenKind.Sequence, string.Empty));
            return;
        }

        if (rawToken.StartsWith("N:", StringComparison.Ordinal))
        {
            var format = rawToken[2..];
            if (format.Length == 0 || format.Any(character => character != '0'))
            {
                errors.Add("{N} only supports zero-fill formats like {N:000}.");
                return;
            }

            tokens.Add(new Token(TokenKind.Sequence, format));
            return;
        }

        if (rawToken == "Date")
        {
            tokens.Add(new Token(TokenKind.Date, DefaultDateFormat));
            return;
        }

        if (rawToken.StartsWith("Date:", StringComparison.Ordinal))
        {
            var format = rawToken[5..];
            if (!IsValidDateFormat(format))
            {
                errors.Add("{Date} only supports dd, MM, yy and yyyy tokens, optionally separated by hyphens.");
                return;
            }

            tokens.Add(new Token(TokenKind.Date, format));
            return;
        }

        errors.Add($"Unknown token {{{rawToken}}}.");
    }

    private static string FormatSequence(int sequence, string format) =>
        format.Length == 0
            ? sequence.ToString(CultureInfo.InvariantCulture)
            : sequence.ToString(new string('0', format.Length), CultureInfo.InvariantCulture);

    private static string FormatDate(DateOnly date, string format)
    {
        var value = date.ToDateTime(TimeOnly.MinValue);
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static bool IsValidDateFormat(string format)
    {
        if (format.Length == 0)
        {
            return false;
        }

        if (format[0] == '-' || format[^1] == '-' || format.Contains("--", StringComparison.Ordinal))
        {
            return false;
        }

        var index = 0;
        while (index < format.Length)
        {
            if (format[index] == '-')
            {
                index++;
                continue;
            }

            var start = index;
            var runCharacter = format[index];
            if (runCharacter is not ('d' or 'M' or 'y'))
            {
                return false;
            }

            while (index < format.Length && format[index] == runCharacter)
            {
                index++;
            }

            var run = format[start..index];
            if (!DateTokens.Contains(run))
            {
                return false;
            }
        }

        return true;
    }

    private enum TokenKind
    {
        Literal,
        Sequence,
        Date
    }

    private sealed record Token(TokenKind Kind, string Value);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskNumberRegex();
}
