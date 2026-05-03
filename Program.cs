using System.Runtime.InteropServices;
using Debugger.Repositories;

namespace Debugger;

/// <summary>
/// Main entry point for the Debugger module test application.
/// </summary>
/// <remarks>
/// <para>
/// This program demonstrates the file processing capabilities of the Debugger module
/// by scanning a directory for VHDL files and parsing each one to identify entities
/// and architectures.
/// </para>
/// <para>
/// The program currently serves as a test harness for the <see cref="Parser"/> class,
/// which is the core component responsible for bridging VHDL source files with the
/// Interpreter module's data models.
/// </para>
/// </remarks>
class Program
{
    /// <summary>
    /// Application entry point that processes all VHDL files in the "Codes" directory.
    /// </summary>
    /// <param name="args">Command-line arguments (currently unused).</param>
    /// <remarks>
    /// <para>
    /// For each VHDL file found in the "Codes" directory:
    /// <list type="number">
    /// <item><description>Creates a new Parser instance with the file path</description></item>
    /// <item><description>Calls ParseFile() to identify entity and architecture blocks</description></item>
    /// <item><description>Reports the number of entities found in the file</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Expected directory structure:
    /// <code>
    /// Debugger/
    /// ├── Program.cs
    /// ├── Repositories/
    /// │   └── Parser.cs
    /// └── Codes/
    ///     ├── test1.vhdl
    ///     ├── test2.vhdl
    ///     └── ...
    /// </code>
    /// </para>
    /// </remarks>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown if the "Codes" directory does not exist.
    /// </exception>
    static void Main(string[] args)
    {
        var files = Directory.GetFiles("Codes");
        foreach (var file in files)
        {
            Console.WriteLine(file);
            Repositories.Parser parser = new(file);
            string? parseError = null;
            try
            {
                parser.ParseFile();
                Console.WriteLine($"Entities found: {parser.Entities.Count}");
            }
            catch (Exception ex)
            {
                // Capture the structural parse error so we can show it
                // AFTER printing the lint diagnostics. Otherwise users see
                // only the throw and miss the missing-`;` warnings that
                // RunDiagnostics has already collected.
                parseError = ex.Message;
            }

            // Surface lint-style diagnostics (missing-`;`, mismatched begin/end,
            // etc.) that ParseFile collected via RunDiagnostics. These were
            // previously populated but never printed, so users couldn't see
            // structural problems in their VHDL until something blew up later.
            foreach (var d in parser.Diagnostics)
            {
                Console.WriteLine($"  [{d.Severity}] L{d.Line}:{d.StartColumn} {d.Code}: {d.Message}");
            }

            if (parseError != null)
            {
                Console.WriteLine($"  [Error] {parseError}");
            }
        }
    }
}