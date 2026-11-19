using Interpreter.Models;
using Interpreter.Services;

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
/// </list>
/// </para>
/// </remarks>
/// <seealso cref="Entity"/>
/// <seealso cref="Architecture"/>
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

    private Parser() => FileContent = string.Empty;

    /// <summary>
    /// Creates a parser over in-memory source text (e.g. an elaborated,
    /// flattened design) so callers never have to round-trip it through a
    /// temporary file.
    /// </summary>
    /// <param name="source">The complete VHDL source to parse.</param>
    public static Parser FromSource(string source) => new() { FileContent = source ?? string.Empty };

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
        var source = new Lint.LintSource(FileContent ?? string.Empty);
        Diagnostics.AddRange(Lint.VhdlLinter.Run(source));

        // Normalize content but keep original 0-based line indices — the
        // tokenizer's error formatter renders them as 1-based "Line N", so
        // we pass the index (i) not the human number (i+1). The lines come
        // without their line comment (string-aware), so a trailing comment on
        // a structural line (`end architecture X;  -- note`) can't hide the
        // ';' the scanner keys on.
        var codeLines = source.Code;
        var content = new List<(int lineNo, string text)>(codeLines.Count);
        for (int i = 0; i < codeLines.Count; i++)
        {
            var trimmed = codeLines[i].Trim();
            if (trimmed.Length == 0) continue;
            content.Add((i, trimmed));
        }

        List<(int line, string text)> rawEntity = new();
        int it = 0;  // State: 0=default, 1=in entity, 2=in architecture
        var configurations = new ConfigurationLineSkipper();
        string currentArchName = string.Empty;
        string currentEntityArchName = string.Empty;
        // Track nested function/procedure depth so `end <name>;` lines for
        // local helpers don't get mis-detected as the architecture's end.
        int funcProcDepth = 0;

        foreach (var entry in content)
        {
            var item = entry.text;
            string comparer = item.ToLower();

            // A configuration declaration (G20) is a design unit of its own: the
            // elaborator applies it; the structural scan skips it.
            if (it == 0 && configurations.Consume(comparer)) continue;

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
                if (((comparer.StartsWith("end ") || comparer == "end;") && comparer.EndsWith(";") && !_excludedEnds.Any(comparer.EndsWith))
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

                // One-line architecture: `architecture X of Y is begin … end
                // [architecture] [X];` — opened and closed on the same line, so
                // the `end`-keyed detection below (which needs a line STARTING
                // with 'end') never fires. The declaration is self-contained, so
                // synthesize it now. (Fix Kmila, don't require a line layout.)
                if (comparer.StartsWith("architecture") && comparer.EndsWith(";")
                    && System.Text.RegularExpressions.Regex.IsMatch(comparer, @"\bbegin\b")
                    && System.Text.RegularExpressions.Regex.IsMatch(comparer, @"\bend\b"))
                {
                    Entity oneLineEntity = Entities.FirstOrDefault(p => p.Name.Equals(currentEntityArchName)) ?? default!;
                    if (oneLineEntity == null) throw EntityNotFound(currentEntityArchName, entry.lineNo);
                    Architecture oneLineArch = new(oneLineEntity.Ports, oneLineEntity.Generics);
                    oneLineArch.Syntetize(rawEntity, cancellationToken);
                    oneLineEntity.SetArchitecture(oneLineArch);
                    rawEntity = new();
                    it = 0;
                    continue;
                }

                // Track function/procedure nesting so `end Adder;` inside the
                // arch declarative region doesn't end the whole architecture.
                if ((comparer.Contains("function ") && comparer.Contains(" is")) ||
                    (comparer.Contains("procedure ") && comparer.Contains(" is")))
                {
                    funcProcDepth++;
                    continue;
                }

                if ((comparer.StartsWith("end ") || comparer == "end;") && comparer.EndsWith(";"))
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

                    // A nested construct end (end if/process/generate/block/...).
                    // These always carry their keyword, so they're never the arch end.
                    if (_excludedEnds.Contains(endName))
                        continue;

                    bool shouldClose;
                    if (endName == "architecture")
                    {
                        // Explicit `end architecture [<label>];` — the keyword makes
                        // this unambiguously the architecture end, so a present label
                        // MUST match the arch (or entity) name; a wrong one is an error.
                        string endLabel = parts.Length >= 3 ? parts[2].TrimEnd(';') : string.Empty;
                        bool labelOk =
                            string.IsNullOrEmpty(endLabel) ||
                            endLabel == currentArchName.ToLower() ||
                            endLabel == currentEntityArchName.ToLower();
                        if (!labelOk)
                            throw new global::Parser.Models.ParserException(
                                $"end label '{endLabel}' does not match architecture '{currentArchName}'",
                                entry.lineNo + 1, 1);
                        shouldClose = true;
                    }
                    else
                    {
                        // Bare `end;` or `end <name>;`. Close only when it matches the
                        // arch/entity name (or is bare). A non-matching bare name may
                        // be an untracked function/procedure end (`function`/`procedure`
                        // aren't in _excludedEnds — they use funcProcDepth), so don't
                        // throw here: fall through and keep collecting as before.
                        shouldClose =
                            string.IsNullOrEmpty(endName) ||
                            endName == currentArchName.ToLower() ||
                            endName == currentEntityArchName.ToLower();
                    }

                    if (shouldClose)
                    {
                        Entity entity = Entities.FirstOrDefault(p => p.Name.Equals(currentEntityArchName)) ?? default!;
                        if (entity == null) throw EntityNotFound(currentEntityArchName, entry.lineNo);

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
    /// An architecture whose entity is neither in this text nor among the
    /// entities seeded from the project's other files (G21); GHDL reports the
    /// unit at the architecture.
    /// </summary>
    private static global::Parser.Models.ParserException EntityNotFound(string entityName, int lineIndex) =>
        new($"entity \"{entityName}\" not found", lineIndex + 1, 1);
}
