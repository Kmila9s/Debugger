using Debugger.Repositories;
using Parser.Repositories;
namespace Debugger;

class Program
{
    static void Main(string[] args)
    {
        List<string> argsConvert = args.ToList();
        if(!argsConvert.Contains("--file"))
            throw new Exception("Expecting file path argument '--file'");
        if(!argsConvert.Contains("--breakPoints"))
            throw new Exception("Expecting file path argument '--breakPoints'");
    
        int fileIndex = argsConvert.IndexOf("--file") + 1;
        string filePath = argsConvert[fileIndex];
        if(!File.Exists(filePath))
            throw new Exception($"File: {filePath} not found");
        Parser.Repositories.Parser parser = new(filePath);
        VariableFetcher fetcher = new(filePath);    
        parser.ParseV2();
        fetcher.ReadFile();
        int bpFileIndex = argsConvert.IndexOf("--breakPoints") + 1;
        string bpFilePath = argsConvert[bpFileIndex];
        if(!File.Exists(bpFilePath))
            throw new Exception($"BP File: {bpFilePath} not found");
        if(!bpFilePath.ToLower().EndsWith("bp"))
            throw new Exception($"Break Point file not detected use a file with 'bp' extention");
        BreakPointFetcher breakPointFetcher = new(filePath, bpFilePath);
        foreach(var item in fetcher.Lines)
            Console.WriteLine($"Line No: {item.Item2} - {item.Item1}");
        breakPointFetcher.ReadFile();
        DebuggerReader debugger = new(breakPointFetcher, fetcher, filePath, parser.Scopes);
        debugger.Debug();
    }
}
