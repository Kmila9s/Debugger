using Parser.Models;

namespace Debugger.Repositories.Lint;

/// <summary>Runs every VHDL lint rule over a source.</summary>
public static class VhdlLinter
{
    private static readonly IVhdlLintRule[] Rules = [new MissingSemicolonRule(), new BlockBalanceRule()];

    /// <summary>All lint findings for <paramref name="source"/>, in rule order.</summary>
    public static List<Diagnostic> Run(LintSource source)
    {
        var findings = new List<Diagnostic>();
        foreach (var rule in Rules) rule.Check(source, findings);
        return findings;
    }

    /// <summary>All lint findings for <paramref name="source"/> text.</summary>
    public static List<Diagnostic> Run(string source) =>
        string.IsNullOrEmpty(source) ? [] : Run(new LintSource(source));
}
