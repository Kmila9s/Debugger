using System.Text.RegularExpressions;
using Parser.Models;

namespace Debugger.Repositories.Lint;

/// <summary>
/// VHD-MISSING-SEMICOLON: a statement that never reaches its <c>;</c> before
/// the next statement starts. A statement may span several lines; only the
/// start of a new statement (or the end of the file) proves the <c>;</c> is
/// missing, so continuation lines are never flagged. Inside parentheses
/// only interface declarations count (ports, generics, parameters): one
/// that is followed by another declaration lacks its <c>;</c>, while the
/// last one is closed by the list's <c>)</c>.
/// </summary>
public sealed partial class MissingSemicolonRule : IVhdlLintRule
{
    private const string Code = "VHD-MISSING-SEMICOLON";
    private const string Message = "Statement appears to be missing a trailing ';'.";

    /// <summary>Line endings (matched against " " + line) after which the statement continues or a block header ends.</summary>
    private static readonly string[] OpenEndings =
        [";", ",", "(", ")", "=>", "&", " is", " then", " else", " loop", " begin", " generate", " select"];

    // Shared regex fragments, so the statement/declaration patterns can't drift apart.
    private const string DeclarationKeywords = "library|use|signal|variable|constant|shared";
    private const string IdentifierList = @"[a-z_]\w*(\s*,\s*[a-z_]\w*)*\s*:(?!=)";

    /// <inheritdoc />
    public void Check(LintSource source, List<Diagnostic> sink)
    {
        // A statement still waiting for its ';': 1-based line + the parenthesis
        // depth it sits at (0 = statement, >0 = element of a port/parameter list).
        (int Line, int Depth)? pending = null;
        int depth = 0;
        for (int i = 0; i < source.Bare.Count; i++)
        {
            var span = source.Bare[i].AsSpan().Trim();
            int depthBefore = depth;
            int depthAfter = depth + span.Count('(') - span.Count(')');
            depth = Math.Max(0, depthAfter);
            if (span.IsEmpty) continue;
            var line = span.ToString();
            bool listDeclaration = depthBefore > 0 && ListDeclaration().IsMatch(line);

            if (pending is { } open)
            {
                bool next = depthBefore == open.Depth && (open.Depth == 0 ? StartsStatement(line) : listDeclaration);
                if (!next)
                {
                    // Continuation; it ends at its ';' or, for the last list element, at the list's ')'.
                    if (line.EndsWith(';') || depthAfter < open.Depth) pending = null;
                    continue;
                }
                sink.Add(source.Warning(open.Line, Code, Message));
                pending = null;
            }

            bool unterminated = depthAfter == depthBefore && !EndsOpen(line) &&
                                (depthBefore == 0 ? LooksLikeStatement(line) : listDeclaration);
            if (unterminated) pending = (i + 1, depthBefore);
        }
        if (pending is { } last) sink.Add(source.Warning(last.Line, Code, Message));
    }

    private static bool EndsOpen(string line)
    {
        var padded = " " + line; // lets " is" / " begin" also match a line that is just the keyword
        foreach (var ending in OpenEndings)
            if (padded.EndsWith(ending, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>Assignments, associations and object/library declarations — statements that end with ';'.</summary>
    private static bool LooksLikeStatement(string line) =>
        line.Contains("<=") || line.Contains(":=") || line.Contains("=>") || DeclarationStart().IsMatch(line);

    /// <summary>True when the line begins a new statement rather than continuing the previous one.</summary>
    private static bool StartsStatement(string line) =>
        KeywordStart().IsMatch(line) || LabelOrDeclarationStart().IsMatch(line) || AssignmentStart().IsMatch(line);

    [GeneratedRegex(@"^(" + DeclarationKeywords + @")\b")]
    private static partial Regex DeclarationStart();

    // `else` and `when` are left out on purpose: they also continue
    // conditional / selected signal assignments split over several lines.
    [GeneratedRegex(@"^(" + DeclarationKeywords + @"|end|begin|process|if|elsif|case|for|while|loop|wait|report|assert|return|type|subtype|component|entity|architecture|package|function|procedure|pure|impure|port|generic|attribute|alias|file|null|exit|next|with|configuration)\b")]
    private static partial Regex KeywordStart();

    [GeneratedRegex("^" + IdentifierList)]
    private static partial Regex LabelOrDeclarationStart();

    [GeneratedRegex(@"^[a-z_][\w.]*(\s*\([^;]*\))?\s*(<=|:=)")]
    private static partial Regex AssignmentStart();

    /// <summary>An interface declaration inside a port/generic/parameter list (<c>[signal] a, b : in t</c>).</summary>
    [GeneratedRegex(@"^((signal|variable|constant|file)\s+)?" + IdentifierList)]
    private static partial Regex ListDeclaration();
}
