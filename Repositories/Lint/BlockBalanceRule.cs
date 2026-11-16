using Parser.Models;

namespace Debugger.Repositories.Lint;

/// <summary>
/// VHD-BLOCK-BALANCE: every block opened (design unit, process, subprogram
/// body, if/case/loop/generate, record, …) must be closed by its own
/// <c>end</c>; <c>end &lt;keyword&gt;</c> must name the block it closes; and
/// <c>begin</c> must appear exactly where the grammar puts it.
/// Walks <see cref="LintSource.Tokens"/> once with a block stack.
/// </summary>
public sealed class BlockBalanceRule : IVhdlLintRule
{
    private const string Code = "VHD-BLOCK-BALANCE";

    private sealed class Block(string kind, int line)
    {
        public string Kind { get; } = kind;
        public int Line { get; } = line;
        public bool HasBegin { get; set; }
    }

    /// <summary>Keyword allowed after <c>end</c> → the block kind it closes (also the kind each opener pushes).</summary>
    private static readonly Dictionary<string, string> BlockKinds = new()
    {
        ["entity"] = "entity", ["architecture"] = "architecture", ["package"] = "package",
        ["configuration"] = "configuration", ["process"] = "process", ["function"] = "subprogram",
        ["procedure"] = "subprogram", ["component"] = "component", ["record"] = "record",
        ["units"] = "units", ["protected"] = "protected", ["block"] = "block", ["if"] = "if",
        ["case"] = "case", ["loop"] = "loop", ["generate"] = "generate", ["for"] = "for",
    };

    /// <summary>Header terminator of each statement opener (besides <c>generate</c>).</summary>
    private static readonly Dictionary<string, string> HeaderTerminator = new()
    {
        ["if"] = "then", ["case"] = "is", ["for"] = "loop", ["while"] = "loop",
    };

    /// <summary>Blocks whose grammar requires <c>begin</c>.</summary>
    private static readonly HashSet<string> RequireBegin = ["architecture", "process", "subprogram", "block"];

    /// <summary>Blocks where <c>begin</c> may appear but need not; every other kind rejects it.</summary>
    private static readonly HashSet<string> OptionalBegin = ["entity", "generate"];

    /// <inheritdoc />
    public void Check(LintSource source, List<Diagnostic> sink)
    {
        var tokens = source.Tokens;
        var stack = new Stack<Block>();
        void Report(int line, string message) => sink.Add(source.Warning(line, Code, message));
        string? At(int k) => k >= 0 && k < tokens.Count ? tokens[k].Text : null;

        for (int i = 0; i < tokens.Count; i++)
        {
            var (text, line) = tokens[i];
            switch (text)
            {
                case "end":
                {
                    int k = At(i + 1) == "postponed" ? i + 2 : i + 1;
                    var keyword = At(k) is { } next && BlockKinds.ContainsKey(next) ? next : null;
                    if (!stack.TryPop(out var block))
                        Report(line, "'end' with no open block to close.");
                    else
                    {
                        if (keyword != null && BlockKinds[keyword] != block.Kind)
                            Report(line, $"'end {keyword}' closes the {Describe(block.Kind)} opened at line {block.Line}.");
                        if (RequireBegin.Contains(block.Kind) && !block.HasBegin)
                            Report(block.Line, $"The {Describe(block.Kind)} opened here has no 'begin'.");
                    }
                    i = StatementEnd(tokens, i);
                    break;
                }
                case "begin":
                    if (!stack.TryPeek(out var top)) Report(line, "'begin' outside any block.");
                    else if (!RequireBegin.Contains(top.Kind) && !OptionalBegin.Contains(top.Kind))
                        Report(line, $"'begin' is not allowed inside the {Describe(top.Kind)} opened at line {top.Line}.");
                    else if (top.HasBegin) Report(line, $"Second 'begin' in the {Describe(top.Kind)} opened at line {top.Line}.");
                    else top.HasBegin = true;
                    break;
                case "entity" or "architecture" or "package" or "configuration" or "function" or "procedure":
                {
                    // A unit/subprogram body has `is` before its first `;`; an
                    // instantiation, a subprogram declaration or `is new` does not open one.
                    int k = HeaderEnd(tokens, i, "is", "is");
                    if (At(k) == "is" && At(k + 1) != "new") stack.Push(new Block(BlockKinds[text], line));
                    i = k;
                    break;
                }
                case "if" or "case" or "for" or "while":
                {
                    int k = HeaderEnd(tokens, i, HeaderTerminator[text], "generate");
                    var terminator = At(k);
                    if (terminator == "generate" || terminator == HeaderTerminator[text])
                        stack.Push(new Block(terminator is "generate" or "loop" ? terminator : text, line));
                    else if (text == "for" && stack.TryPeek(out var parent) && parent.Kind is "configuration" or "for")
                    {
                        stack.Push(new Block("for", line)); // block configuration: for … end for;
                        break;
                    }
                    i = k;
                    break;
                }
                case "loop" or "process" or "record" or "units" or "protected":
                    stack.Push(new Block(text, line));
                    if (text == "protected" && At(i + 1) == "body") i++;
                    break;
                case "component":
                    if (At(i - 1) != ":") stack.Push(new Block("component", line)); // `u1 : component c port map …` instantiates
                    break;
                case "block":
                    if (At(i - 1) == ":") stack.Push(new Block("block", line));
                    break;
            }
        }

        foreach (var block in stack)
            Report(block.Line, $"The {Describe(block.Kind)} opened here is never closed with 'end'.");
    }

    private static string Describe(string kind) => kind == "subprogram" ? "subprogram body" : $"'{kind}'";

    /// <summary>
    /// Index of the first <paramref name="first"/>/<paramref name="second"/> after
    /// <paramref name="start"/> at parenthesis depth 0, or else of the <c>;</c>
    /// that ends the statement at depth 0 (the <c>;</c> between parameters
    /// doesn't). A <c>begin</c>/<c>end</c> — never inside a parameter list —
    /// also stops the scan, so an unclosed <c>(</c> can't run it to the end of
    /// the file. The caller jumps to the result: every token is visited once.
    /// </summary>
    private static int HeaderEnd(IReadOnlyList<LintToken> tokens, int start, string first, string second)
    {
        int depth = 0;
        for (int k = start + 1; k < tokens.Count; k++)
        {
            var text = tokens[k].Text;
            if (text is "begin" or "end") return k - 1;
            if (text == ";" && depth <= 0) return k;
            if (text == "(") depth++;
            else if (text == ")") depth--;
            else if (depth <= 0 && (text == first || text == second)) return k;
        }
        return tokens.Count - 1;
    }

    private static int StatementEnd(IReadOnlyList<LintToken> tokens, int start)
    {
        for (int k = start + 1; k < tokens.Count; k++)
            if (tokens[k].Text == ";") return k;
        return tokens.Count;
    }
}
