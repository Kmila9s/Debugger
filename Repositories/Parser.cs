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
    /// Keywords that should not be treated as end-block markers.
    /// </summary>
    /// <remarks>
    /// When encountering "end" followed by one of these keywords, the parser
    /// continues collecting lines rather than ending the current block.
    /// This prevents "end if;", "end when;", and "end process;" from being
    /// mistaken for entity or architecture end statements.
    /// </remarks>
    private List<string> _excludedEnds = new()
    {
        "if", "when", "process"
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
    public void ParseFile()
    {
        // Note: Tokenizer is instantiated but not actively used in current implementation.
        // Reserved for future enhancement to provide more robust parsing.
        Tokenizer tokenizer = new("");

        // Normalize content: split by newlines, trim whitespace, remove empty lines
        string[] content = FileContent.Split("\n")
                            .Select(p => p.Trim())
                            .Where(p => !String.IsNullOrEmpty(p) && p != " ")
                            .ToArray();

        List<string> rawEntity = new();
        int it = 0;  // State: 0=default, 1=in entity, 2=in architecture
        string currentEntityArchName = string.Empty;

        foreach (var item in content)
        {
            string comparer = item.Trim().ToLower();

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
                currentEntityArchName = item.Split(' ')[3];
                it = 2;
            }

            // State 1: Collecting entity lines
            if (it == 1)
            {
                // Check for entity end: "end EntityName;" or "end entity;"
                // Exclude "end if;", "end when;", "end process;"
                if (comparer.StartsWith("end ") && comparer.EndsWith(";") && !_excludedEnds.Any(comparer.EndsWith))
                {
                    rawEntity.Add(item.Trim());
                    Entity entity = new(rawEntity.ToArray());
                    Entities.Add(entity);
                    rawEntity = new();
                    it = 0;
                }
                else
                {
                    rawEntity.Add(item);
                }
            }
            // State 2: Collecting architecture lines
            else if (it == 2)
            {
                string clean = comparer.TrimEnd(';');
                // Check for architecture end: "end ArchName;" or "end architecture;"
                if (comparer.StartsWith("end ") && !_excludedEnds.Any(clean.EndsWith))
                {
                    rawEntity.Add(item);

                    // Find the entity this architecture belongs to
                    Entity entity = Entities.FirstOrDefault(p => p.Name.Equals(currentEntityArchName)) ?? default!;
                    if (entity == null) throw new Exception("Entity not found");

                    // Create architecture and attach to entity
                    Architecture arch = new(entity.Ports, entity.Generics);
                    arch.Syntetize(rawEntity.ToArray());
                    entity.SetArchitecture(arch);
                    it = 0;
                }
                else
                {
                    rawEntity.Add(item);
                }
            }
        }
    }
}