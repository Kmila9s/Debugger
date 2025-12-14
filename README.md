# Debugger Module

## Project: Kmila-9s

This module is a core component of **Kmila-9s**, a project officially titled "Aplicación Multiplataforma para el Aprendizaje y Depuración de Código VHDL sin Requerimiento de Hardware Físico" (Multiplatform Application for Learning and Debugging VHDL Code without Requiring Physical Hardware).

**Author:** Ulrich Tamayo Daniel
**Email:** dulricht1700@alumno.ipn.mx
**Email 2:** danielulrichtamayo@gmail.com
**Version:** 0.6.0
**Last Updated:** December 13, 2025

---

## 1. Project Overview

The primary goal of this project is to develop a free, open-source, and multiplatform desktop application for the interactive debugging of VHDL code. The application aims to facilitate the learning of digital design by providing a unified environment for simulation and real-time visualization of digital signals, clock cycles, and port states, completely eliminating the need for physical hardware like FPGAs.

### Key Motivations
*   **Accessibility:** Overcoming the financial barrier of expensive FPGA boards and proprietary software licenses.
*   **Pedagogical Improvement:** Offering an intuitive, integrated graphical interface that simplifies the debugging process, unlike the fragmented command-line workflows of tools like GHDL and GTKWave.
*   **Flexibility:** Providing a self-contained application that runs locally on multiple operating systems (Windows, macOS, Linux, Android, iOS) without requiring an internet connection.

This project is built using .NET and MAUI, ensuring a modern and maintainable codebase with broad platform support.

## 2. The Debugger Module

This specific module, the **Debugger**, acts as the central orchestrator for the VHDL simulation process. It is responsible for taking raw VHDL source files, preparing them for execution, and (ultimately) managing the interactive debugging session. It integrates the `Parser`, `Interpreter`, and `TimeMachine` modules to create a cohesive simulation environment.

### Core Responsibilities
*   **VHDL File Processing:** Reading raw `.vhdl` source files from the filesystem.
*   **Structural Analysis:** Parsing the file content to identify top-level `entity` and `architecture` blocks.
*   **Data Model Hydration:** Constructing the in-memory C# objects (like `Entity` and `Architecture` from the `Interpreter` module) that are required for the simulation engine to work.
*   **Simulation Orchestration:** (Future) Integrating with the `Interpreter` and `TimeMachine` to control the step-by-step execution of the VHDL code.
*   **State Inspection:** (Future) Providing the hooks needed to inspect signal and variable states, manage breakpoints, and visualize data.

## 3. Architecture of the Debugger Module

The Debugger module is designed as a high-level coordinator that prepares data for the other core components of the Kmila-9s project.

### Key Components

*   **`Program.cs`**: The main entry point for the application. It is currently responsible for scanning a directory of VHDL files and initiating the parsing process for each one.

*   **`Repositories/Parser.cs`**: A specialized file processor that acts as a bridge between VHDL source code and the `Interpreter` module. It reads the content of a `.vhdl` file, uses string manipulation to identify the boundaries of `entity` and `architecture` blocks, and uses this information to instantiate the `Entity` and `Architecture` data models required by the interpreter.

### Core Data Models

This module primarily uses and creates data models defined in the **Interpreter Module**, including:
*   **`Interpreter.Models.Entity`**: Represents a VHDL entity, including its ports and generics.
*   **`Interpreter.Services.Architecture`**: Represents a VHDL architecture, containing the logic to be simulated.

## 4. How to Use This Module

To use the debugger's parsing functionality, you provide the path to a VHDL file to the `Debugger.Repositories.Parser`. The `ParseFile()` method then processes the file and populates its `Entities` list.

```csharp
using Debugger.Repositories;
using System;

// This example demonstrates the current file processing capability.
var files = Directory.GetFiles("Codes");
foreach (var file in files)
{
    try
    {
        // 1. Initialize the Debugger's Parser with a file path.
        Parser vhdlFileParser = new(file);

        // 2. Process the file to identify entities and architectures.
        vhdlFileParser.ParseFile();

        // 3. The parsed entities are now available for the Interpreter.
        Console.WriteLine($"File: {file}, Entities found: {vhdlFileParser.Entities.Count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while parsing {file}: {ex.Message}");
    }
}
```

## 5. Dependencies

This module integrates other key parts of the Kmila-9s project and relies on:
*   **`Interpreter` Module**: For its core data models (`Entity`, `Architecture`).
*   **`TimeMachine` Module**: (For future integration) To control the simulation timeline.
*   **`Parser` Module**: For the low-level `Tokenizer`.
*   **`Microsoft.Extensions.Logging`**: For logging and diagnostics.

## 6. Debugger Flow and Class Relationships

The following diagrams illustrate the current data flow and class relationships for the file processing stage.

### 6.1. High-Level Data Flow

This diagram shows how the Debugger processes VHDL files to prepare them for the interpreter.

```mermaid
graph TD
    A["VHDL Files (.vhdl)"] --> B["Debugger.Repositories.Parser"];
    B -->|Identifies entities & architectures| C{"Interpreter Models (Entity, Architecture)"};
    C --> D["Interpreter Engine"];

    style B fill:#ccf,stroke:#333,stroke-width:2px
    style D fill:#bbf,stroke:#333,stroke-width:2px
```

### 6.2. Detailed Class Diagram

This diagram shows the key classes and their relationship with other modules.

```mermaid
classDiagram
    direction LR

    class Program {
        +static Main()
    }

    class DbgParser["Debugger.Repositories.Parser"] {
        +FileContent : string
        +Entities : List~Entity~
        +ParseFile()
    }

    class IntEntity["Interpreter.Models.Entity"] {
        <<External>>
    }
    
    class IntArch["Interpreter.Services.Architecture"] {
        <<External>>
    }

    class Tok["Parser.Repositories.Tokenizer"] {
        <<External>>
    }

    Program --> DbgParser : creates and uses
    DbgParser --> IntEntity : creates
    DbgParser --> IntArch : creates
    DbgParser --> Tok : uses
```

### 6.3. Sequence Diagram: Parsing a File

This diagram shows the runtime interactions when `Program.cs` processes a single VHDL file.

```mermaid
sequenceDiagram
    participant Main
    participant DbgParser as "Debugger.Parser"
    participant FileIO as "File IO"

    Main->>DbgParser: new(filePath)
    activate DbgParser
    DbgParser->>FileIO: ReadAllText(filePath)
    FileIO-->>DbgParser: returns file content
    deactivate DbgParser
    
    Main->>DbgParser: ParseFile()
    activate DbgParser
    
    loop For each line in content
        DbgParser->>DbgParser: Identify `entity` and `architecture` blocks
    end
    
    DbgParser->>DbgParser: Create Entity and Architecture objects
    
    deactivate DbgParser
    DbgParser-->>Main: Entities list is populated
```

### 6.4. Module Diagram
```mermaid
graph TD

    user["User<br>[External]"]
    vhdl_code["VHDL Code Files<br>/Codes/"]
    subgraph debugger_system["VHDL Debugger<br>[External]"]
        subgraph debugger_app_boundary["Debugger Application<br>[External]"]
            main_logic["Main Application Logic<br>/Program.cs"]
            ui["User Interface<br>[External]"]
            %% Edges at this level (grouped by source)
            ui["User Interface<br>[External]"] -->|"Sends commands to"| main_logic["Main Application Logic<br>/Program.cs"]
        end
        subgraph parser_boundary["Parser<br>[External]"]
            vhdl_parser["VHDL Parser<br>/Repositories/Parser.cs"]
        end
        subgraph interpreter_boundary["Interpreter<br>[External]"]
            vhdl_interpreter["VHDL Interpreter<br>[External]"]
            tokenizer["Tokenizer<br>/Interpreter.Services.Tokenizer (referenced in Parser.cs)"]
            vhdl_models["VHDL Models<br>/Interpreter.Models (referenced in Parser.cs)"]
            %% Edges at this level (grouped by source)
            vhdl_interpreter["VHDL Interpreter<br>[External]"] -->|"Uses | VHDL data structures"| vhdl_models["VHDL Models<br>/Interpreter.Models (referenced in Parser.cs)"]
        end
        subgraph time_machine_boundary["Time Machine<br>[External]"]
            state_manager["State Manager<br>[External]"]
            time_travel_logic["Time Travel Logic<br>[External]"]
            %% Edges at this level (grouped by source)
            time_travel_logic["Time Travel Logic<br>[External]"] -->|"Retrieves states from"| state_manager["State Manager<br>[External]"]
        end
        %% Edges at this level (grouped by source)
        main_logic["Main Application Logic<br>/Program.cs"] -->|"Invokes"| vhdl_parser["VHDL Parser<br>/Repositories/Parser.cs"]
        main_logic["Main Application Logic<br>/Program.cs"] -->|"Controls"| vhdl_interpreter["VHDL Interpreter<br>[External]"]
        main_logic["Main Application Logic<br>/Program.cs"] -->|"Manages"| state_manager["State Manager<br>[External]"]
        vhdl_parser["VHDL Parser<br>/Repositories/Parser.cs"] -->|"Uses | VHDL tokens"| tokenizer["Tokenizer<br>/Interpreter.Services.Tokenizer (referenced in Parser.cs)"]
        vhdl_parser["VHDL Parser<br>/Repositories/Parser.cs"] -->|"Creates | VHDL AST"| vhdl_models["VHDL Models<br>/Interpreter.Models (referenced in Parser.cs)"]
        vhdl_interpreter["VHDL Interpreter<br>[External]"] -->|"Consumes | VHDL AST"| vhdl_parser["VHDL Parser<br>/Repositories/Parser.cs"]
        state_manager["State Manager<br>[External]"] -->|"Captures states from"| vhdl_interpreter["VHDL Interpreter<br>[External]"]
    end
    %% Edges at this level (grouped by source)
    user["User<br>[External]"] -->|"Interacts with"| ui["User Interface<br>[External]"]
    vhdl_parser["VHDL Parser<br>/Repositories/Parser.cs"] -->|"Reads | VHDL files"| vhdl_code["VHDL Code Files<br>/Codes/"]

```

## 7. Current Limitations

The following limitations exist in the current implementation:

*   **Tokenizer Not Actively Used:** The `Tokenizer` class from the Parser module is instantiated in `ParseFile()` but not actively used. This is reserved for future enhancement to provide more robust parsing.
*   **No Multiline Comment Handling:** The parser uses simple line-by-line string matching and does not handle VHDL multiline comments (`/* ... */`).
*   **Simple Parsing Approach:** Structure detection relies on string pattern matching rather than true AST-based parsing.
*   **Single Entity-Architecture Assumption:** The parser expects architectures to reference entities that were declared earlier in the same file.

## 8. Changelog

### Version 0.6.0 (2025-12-13)

#### Documentation Improvements

*   **Program.cs**: Added comprehensive XML documentation including:
    *   Class-level summary explaining the test harness purpose
    *   Method documentation with processing steps
    *   Expected directory structure diagram
    *   Exception documentation

*   **Repositories/Parser.cs**: Added complete XML documentation including:
    *   Class-level documentation explaining the state machine algorithm
    *   Detailed remarks about parsing limitations
    *   Property documentation for `FileContent` and `Entities`
    *   Private field documentation for `_excludedEnds`
    *   Constructor and method documentation with step-by-step processing details
    *   Inline comments explaining state transitions and entity/architecture association

*   **README.md**: Added version information, last updated date, current limitations section, and this changelog

---
*Last Updated: December 13, 2025*
