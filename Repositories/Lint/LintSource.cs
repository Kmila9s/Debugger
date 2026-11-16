using System.Text.RegularExpressions;
using Parser.Models;

namespace Debugger.Repositories.Lint;

/// <summary>
/// One VHDL source prepared once for every lint rule (and for the parser's
/// line pass). Index <c>i</c> of each line list is source line <c>i + 1</c>.
/// </summary>
public sealed partial class LintSource
{
    /// <summary>Prepares <paramref name="source"/> (CRLF tolerated).</summary>
    public LintSource(string source)
    {
        Raw = source.Split(["\r\n", "\n"], StringSplitOptions.None);
        Code = Raw.Select(VhdlText.StripLineComment).ToArray();
        Bare = Code.Select(c => CharLiteral().Replace(VhdlText.MaskStringLiterals(c), "   ").ToLowerInvariant()).ToArray();
        Tokens = Tokenize(Bare);
    }

    /// <summary>Lines as written.</summary>
    public IReadOnlyList<string> Raw { get; }

    /// <summary>Lines without their line comment (string-aware).</summary>
    public IReadOnlyList<string> Code { get; }

    /// <summary>
    /// <see cref="Code"/> lower-cased, with string contents and character
    /// literals masked (length kept), so keywords, parentheses and <c>;</c>
    /// inside literals never count.
    /// </summary>
    public IReadOnlyList<string> Bare { get; }

    /// <summary>Identifiers/keywords and <c>; : ( )</c> of <see cref="Bare"/>, in order.</summary>
    public IReadOnlyList<LintToken> Tokens { get; }

    /// <summary>A warning spanning the code part of 1-based <paramref name="line"/>.</summary>
    public Diagnostic Warning(int line, string code, string message)
    {
        int index = Math.Clamp(line, 1, Raw.Count) - 1;
        var raw = Raw[index];
        return new Diagnostic
        {
            Severity    = DiagnosticSeverity.Warning,
            Line        = line,
            StartColumn = raw.Length - raw.TrimStart().Length + 1,
            EndColumn   = Math.Max(1, Code[index].TrimEnd().Length) + 1,
            Code        = code,
            Message     = message,
        };
    }

    private static LintToken[] Tokenize(IReadOnlyList<string> bare)
    {
        var tokens = new List<LintToken>();
        for (int i = 0; i < bare.Count; i++)
            foreach (Match m in Word().Matches(bare[i]))
                tokens.Add(new LintToken(m.Value, i + 1));
        return tokens.ToArray();
    }

    // A character literal, not an attribute tick (clk'event) or a qualified
    // expression (t'(…)): the quote must not follow an identifier or ')'.
    [GeneratedRegex(@"(?<![A-Za-z0-9_)\]])'.'")]
    private static partial Regex CharLiteral();

    [GeneratedRegex(@"[a-z_][a-z0-9_]*|[;:()]")]
    private static partial Regex Word();
}

/// <summary>One lint token: lower-cased text and its 1-based source line.</summary>
public readonly record struct LintToken(string Text, int Line);
