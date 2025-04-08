using Debugger.Interfaces;

namespace Debugger.Repositories;
public class VariableFetcher : IFetcher{
    private readonly string _filePath = string.Empty;
    public List<(string, int)> Lines { get; private set; } = new();
    public VariableFetcher(string filePath){
        _filePath = filePath;
    }
    public void ReadFile(){
        using StreamReader reader = new(_filePath);
        int counter = 0;
        var line = reader.ReadLine();
        if(line != null){
            while(line != null){
                if(!line.Trim().StartsWith("--") && !String.IsNullOrEmpty(line))
                    Lines.Add((line.Trim(), counter++));
                line = reader.ReadLine();
            }
        }
    }
}