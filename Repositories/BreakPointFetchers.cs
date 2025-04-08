using Debugger.Interfaces;

namespace Debugger.Repositories;
public class BreakPointFetcher : IFetcher {
    private readonly string _filePath = string.Empty, _bpFile = string.Empty;
    private int _fileLines = 0;
    public List<(string, int)> Bps { get; private set; } = new();
    public Dictionary<int, string> BreakPoints { get; private set;} = new();
    public BreakPointFetcher(string filePath, string bpFile){
        _filePath = filePath;
        _bpFile = bpFile;
        if(!_bpFile.EndsWith(".bp")) throw new Exception("File not compatible");
        VerifyLines();
    }
    public void ReadFile(){
        using StreamReader reader = new(_bpFile);
        var line = reader.ReadLine();
        if(line != null){
            while(line != null){
                line = line.Trim();
                var items = line.Split(" ");
                string func = items[0];
                int lineNum = Convert.ToInt32(items[1]);
                if(lineNum > _fileLines) throw new Exception("Break point line higher than file lines");
                BreakPoints.Add(lineNum, func);
                Bps.Add((func, lineNum));
                line = reader.ReadLine();
            }
        }
    }
    private void VerifyLines(){
        using StreamReader reader = new(_filePath);
        int counter = 0;
        var line = reader.ReadLine();
        if(line != null){
            while(line != null){
                counter++;
                line = reader.ReadLine();
            }
        }
        _fileLines = counter;
    }
}