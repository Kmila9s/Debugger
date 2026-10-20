using Interpreter.Models;
using Interpreter.Services;
using Parser.Repositories;

namespace Debugger.Repositories;

/// <summary>
/// Bridge between VHDL source files and the Interpreter module's data models.
/// </summary>
/// <remarks>
/// <para>
/// The Parser class is responsible for reading VHDL source files and extracting
/// structural information to create <see cref="Entity"/> and <see cref="Architecture"/>
/// objects that can be used by the Interpreter module for simulation.
/// </para>
/// <para>
/// <b>Parsing Algorithm:</b>
/// The parser uses a simple state machine approach with three states:
/// <list type="bullet">
/// <item><description><c>it=0</c>: Default state - looking for entity or architecture keywords</description></item>
/// <item><description><c>it=1</c>: Inside entity block - collecting lines until "end" keyword</description></item>
/// <item><description><c>it=2</c>: Inside architecture block - collecting lines until "end" keyword</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Limitations:</b>
/// <list type="bullet">
/// <item><description>Uses string matching rather than full tokenization for structure detection</description></item>
/// <item><description>Does not handle multiline comments</description></item>
/// <item><description>The Tokenizer is instantiated but not actively used (reserved for future enhancement)</description></item>
/// </list>
/// </para>
/// </remarks>
/// <seealso cref="Entity"/>
/// <seealso cref="Architecture"/>
/// <seealso cref="Tokenizer"/>
public class Parser
{
    /// <summary>
    /// Gets the raw content of the VHDL file.
    /// </summary>
    /// <value>The complete text content of the VHDL file as read from disk.</value>
    public string FileContent { get; private set; }

    /// <summary>
    /// Gets the list of parsed Entity objects.
    /// </summary>
    /// <value>
    /// A list of <see cref="Entity"/> objects created from the VHDL file.
    /// Each entity includes its associated architecture after successful parsing.
    /// </value>
    public List<Entity> Entities { get; private set; } = new();

    /// <summary>
    /// Structured diagnostics collected during the most recent <see cref="ParseFile()"/>
    /// call. Errors that used to propagate as a <c>ParserException</c>
    /// still do — but we also mirror them here so the Editor can surface
    /// line-accurate Monaco markers without catching exceptions.
    /// Warnings (missing semicolons, style lapses, etc.) are pushed here and
    /// don't interrupt the parse path.
    /// </summary>
    public List<global::Parser.Models.Diagnostic> Diagnostics { get; private set; } = new();

    /// <summary>
    /// Keywords that should not be treated as end-block markers when they
    /// follow <c>end</c>. Without these, lines like <c>end loop;</c> inside a
    /// for-loop or <c>end case;</c> inside a process body would prematurely
    /// terminate the architecture and feed Synthesize() a truncated chunk.
    /// </summary>
    private List<string> _excludedEnds = new()
    {
        "if", "when", "process", "loop", "case", "generate", "record",
        "component", "block", "units"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="Parser"/> class.
    /// </summary>
    /// <param name="filePath">The path to the VHDL file to parse.</param>
    /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist.</exception>
    /// <remarks>
    /// The constructor reads the entire file content into memory during initialization.
    /// </remarks>
    public Parser(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException();
        FileContent = File.ReadAllText(filePath);
    }

    /// <summary>
    /// Parses the VHDL file content to identify and create Entity and Architecture objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Processing Steps:</b>
    /// <list type="number">
    /// <item><description>Split file content into lines and normalize whitespace</description></item>
    /// <item><description>Iterate through lines using state machine (it variable)</description></item>
    /// <item><description>When "entity" keyword found, switch to entity collection mode (it=1)</description></item>
    /// <item><description>When "architecture" keyword found, extract entity name and switch to architecture mode (it=2)</description></item>
    /// <item><description>Collect lines until appropriate "end" keyword is found</description></item>
    /// <item><description>Create Entity or Architecture objects from collected lines</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Entity/Architecture Association:</b>
    /// The architecture declaration specifies which entity it belongs to using the syntax:
    /// <c>architecture ArchName of EntityName is</c>. The parser extracts the entity name
    /// (at index 3 after splitting by space) and looks it up in the Entities list.
    /// </para>
    /// </remarks>
    /// <exception cref="Exception">
    /// Thrown with message "Entity not found" if an architecture references an entity
    /// that was not previously parsed from the file.
    /// </exception>
    public void ParseFile() => ParseFile(CancellationToken.None);

    /// <summary>
    /// F-G-08 pass 4 (2026-07-17): cancellable overload. Threads the token
    /// through both <c>entity.Syntetize</c> and <c>arch.Syntetize</c> calls
    /// so the wall-timeout hook set by <c>KMILA_SIM_WALL_TIMEOUT_MS</c> in
    /// <c>HeadlessSimulator</c> can actually break a stuck synthesis descent
    /// instead of waiting for the outer HTTP client to disconnect.
    /// </summary>
    public void ParseFile(CancellationToken cancellationToken)
    {
        // Run the source-level lint pass FIRST so its warning diagnostics are
        // populated even when the structural parse below throws (missing
        // `end`, unresolved entity reference, etc). Without this, any fatal
        // parse error blanked the Diagnostics list and students saw no
        // Monaco squiggles at all on broken files.
        RunDiagnostics(FileContent);

        // Note: Tokenizer is instantiated but not actively used in current implementation.
        // Reserved for future enhancement to provide more robust parsing.
        Tokenizer tokenizer = new("");

        // Normalize content but keep original 0-based line indices — the
        // tokenizer's error formatter renders them as 1-based "Line N", so
        // we pass the index (i) not the human number (i+1).
        var rawLines = FileContent.Split("\n");
        var content = new List<(int lineNo, string text)>(rawLines.Length);
        for (int i = 0; i < rawLines.Length; i++)
        {
            var trimmed = rawLines[i].Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed == " ") continue;
            content.Add((i, trimmed));
        }

        List<(int line, string text)> rawEntity = new();
        int it = 0;  // State: 0=default, 1=in entity, 2=in architecture
        string currentArchName = string.Empty;
        string currentEntityArchName = string.Empty;
        // Track nested function/procedure depth so `end <name>;` lines for
        // local helpers don't get mis-detected as the architecture's end.
        int funcProcDepth = 0;

        foreach (var entry in content)
        {
            var item = entry.text;
            string comparer = item.ToLower();

            // Detect start of entity block
            if (comparer.StartsWith("entity"))
            {
                it = 1;
            }
            // Detect start of architecture block and extract associated entity name
            else if (comparer.StartsWith("architecture"))
            {
                // Format: "architecture ArchName of EntityName is"
                // Index:       0           1      2      3       4
                var archParts = item.Split(' ');
                currentArchName = archParts.Length > 1 ? archParts[1] : string.Empty;
                currentEntityArchName = archParts.Length > 3 ? archParts[3] : string.Empty;
                it = 2;
                funcProcDepth = 0;
            }

            // State 1: Collecting entity lines
            if (it == 1)
            {
                // Check for entity end: "end EntityName;" or "end entity;"
                // Exclude "end if;", "end when;", "end process;"
                // One-line entity (`entity X is port (...); end entity X;`): the
                // whole declaration, including its closing `end`, is on the same
                // line that opened it. An entity declarative region has no nested
                // `end`, so a standalone `end` word on an `entity …;` line always
                // closes it — fix the scanner rather than demand a line layout.
                bool inlineEntityEnd = comparer.StartsWith("entity") && comparer.EndsWith(";")
                    && System.Text.RegularExpressions.Regex.IsMatch(comparer, @"\bend\b");
                if ((comparer.StartsWith("end ") && comparer.EndsWith(";") && !_excludedEnds.Any(comparer.EndsWith))
                    || inlineEntityEnd)
                {
                    rawEntity.Add((entry.lineNo, item.Trim()));
                    var entity = new Entity();
                    // F-G-08 pass 4: use the tuple-list + ct overload so wall-timeout can break out.
                    entity.Syntetize(rawEntity, cancellationToken);
                    Entities.Add(entity);
                    rawEntity = new();
                    it = 0;
                }
                else
                {
                    rawEntity.Add((entry.lineNo, item));
                }
            }
            // State 2: Collecting architecture lines
            else if (it == 2)
            {
                rawEntity.Add((entry.lineNo, item));

                // Track function/procedure nesting so `end Adder;` inside the
                // arch declarative region doesn't end the whole architecture.
                if ((comparer.Contains("function ") && comparer.Contains(" is")) ||
                    (comparer.Contains("procedure ") && comparer.Contains(" is")))
                {
                    funcProcDepth++;
                    continue;
                }

                if (comparer.StartsWith("end ") && comparer.EndsWith(";"))
                {
                    var parts = comparer.Split(' ');
                    string endName = parts.Length >= 2 ? parts[1].TrimEnd(';') : string.Empty;

                    // Inside a function/procedure: `end function;` or `end <fn_name>;`
                    // closes one nesting level. Don't treat as arch end yet.
                    if (funcProcDepth > 0)
                    {
                        if (endName == "function" || endName == "procedure" || !_excludedEnds.Contains(endName))
                        {
                            funcProcDepth--;
                        }
                        continue;
                    }

                    // At depth 0: it's the arch end IF the suffix matches the
                    // arch name, the entity name, or is the bare `end;`.
                    if (_excludedEnds.Contains(endName))
                        continue;

                    bool matchesArch =
                        endName == currentArchName.ToLower() ||
                        endName == currentEntityArchName.ToLower() ||
                        endName == "architecture" ||
                        string.IsNullOrEmpty(endName); // `end;`

                    if (matchesArch)
                    {
                        Entity entity = Entities.FirstOrDefault(p => p.Name.Equals(currentEntityArchName)) ?? default!;
                        if (entity == null) throw new Exception("Entity not found");

                        Architecture arch = new(entity.Ports, entity.Generics);
                        // F-G-08 pass 4: use the tuple-list + ct overload so wall-timeout can break out.
                        arch.Syntetize(rawEntity, cancellationToken);
                        entity.SetArchitecture(arch);
                        rawEntity = new();
                        it = 0;
                    }
                }
            }
        }

    }

    /// <summary>
    /// Lightweight source-level validator run after <see cref="ParseFile()"/>.
    /// Emits warning-level diagnostics for common student mistakes:
    /// <list type="bullet">
    ///   <item><description>Statement-ending lines without the expected semicolon.</description></item>
    ///   <item><description>Mismatched begin / end depth.</description></item>
    /// </list>
    /// Runs character-by-character on the ORIGINAL <see cref="FileContent"/>
    /// (not the whitespace-collapsed array used by ParseFile) so the
    /// line/column coords it reports still match what the student sees in
    /// Monaco.
    /// </summary>
    private void RunDiagnostics(string source)
    {
        if (string.IsNullOrEmpty(source)) return;

        var lines = source.Replace("\r\n", "\n").Split('\n');
        int beginDepth = 0, endDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            // Strip VHDL line comments (-- to end-of-line) before analysis.
            var commentStart = raw.IndexOf("--", StringComparison.Ordinal);
            var effective = commentStart >= 0 ? raw[..commentStart] : raw;
            var trimmed = effective.TrimEnd();
            if (trimmed.Length == 0) continue;

            var lower = trimmed.ToLowerInvariant();

            // Track begin/end balance — mismatched totals surface at the
            // end of the file as a single diagnostic.
            if (lower == "begin" || lower.EndsWith(" begin"))
                beginDepth++;
            if (lower.StartsWith("end ") || lower == "end" || lower.StartsWith("end;"))
                endDepth++;

            // Missing-semicolon detector — fires on any statement-looking
            // line that doesn't terminate properly. Skips block headers
            // (entity/architecture/process/…), labels, and comments.
            if (LooksLikeStatement(lower) && !trimmed.EndsWith(";") && !trimmed.EndsWith(",") &&
                !trimmed.EndsWith("is") && !trimmed.EndsWith("then") &&
                !trimmed.EndsWith("else") && !trimmed.EndsWith("loop") &&
                !trimmed.EndsWith("begin") && !trimmed.EndsWith("generate") &&
                !trimmed.EndsWith("(") && !trimmed.EndsWith(")"))
            {
                Diagnostics.Add(new global::Parser.Models.Diagnostic
                {
                    Severity    = global::Parser.Models.DiagnosticSeverity.Warning,
                    Line        = i + 1,
                    StartColumn = raw.Length - raw.TrimStart().Length + 1,
                    EndColumn   = Math.Max(1, effective.Length) + 1,
                    Code        = "VHD-MISSING-SEMICOLON",
                    Message     = "Statement appears to be missing a trailing ';'. The simulator tolerates this but your synthesizer won't."
                });
            }
        }

        if (beginDepth != endDepth)
        {
            Diagnostics.Add(new global::Parser.Models.Diagnostic
            {
                Severity    = global::Parser.Models.DiagnosticSeverity.Warning,
                Line        = Math.Max(1, lines.Length),
                StartColumn = 1,
                EndColumn   = 1,
                Code        = "VHD-BLOCK-BALANCE",
                Message     = $"Block mismatch: {beginDepth} 'begin' keyword(s) vs {endDepth} 'end' keyword(s). Double-check your block closers."
            });
        }
    }

    /// <summary>
    /// Heuristic: does this stripped-and-lower-cased line look like a
    /// statement that ought to end with a semicolon? Rules out declarations
    /// whose terminators are handled by block parsers (entity/architecture/
    /// process/if/case/when), library imports, and obvious keywords.
    /// </summary>
    private static bool LooksLikeStatement(string lower)
    {
        if (lower.Length == 0) return false;
        // Signal / variable assignments, port assignments, procedure calls.
        if (lower.Contains("<=") || lower.Contains(":=") || lower.Contains("=>"))
            return true;
        // Library / use / signal / variable / constant declarations.
        if (lower.StartsWith("library ") || lower.StartsWith("use ") ||
            lower.StartsWith("signal ") || lower.StartsWith("variable ") ||
            lower.StartsWith("constant "))
            return true;
        return false;
    }
}