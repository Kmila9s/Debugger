using Parser.Models;

namespace Debugger.Repositories.Lint;

/// <summary>One structural lint check over a <see cref="LintSource"/>.</summary>
public interface IVhdlLintRule
{
    /// <summary>Appends the rule's findings to <paramref name="sink"/>.</summary>
    void Check(LintSource source, List<Diagnostic> sink);
}
