using Parser.Models;
using Microsoft.Extensions.Logging;

namespace Debugger.Repositories;
public class DebuggerReader {
    private readonly BreakPointFetcher _breakPoints;
    private readonly VariableFetcher _variableFetcher;
    private readonly Dictionary<string, HashSet<Variable>> _scopes;
    private string _fileName = string.Empty, _currentScope = string.Empty;
    private const string DEFAULT_COMMANDS = "1) Continue\n2) Next step\n3) Step into\n";
    private readonly ILogger<DebuggerReader> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DebuggerReader>();
    public DebuggerReader(BreakPointFetcher breakPointFetcher, VariableFetcher fetcher, string FileName, Dictionary<string, HashSet<Variable>> Scopes) {
        _breakPoints = breakPointFetcher;
        _fileName = FileName;
        _variableFetcher = fetcher;
        _scopes = Scopes;
    }
    public void PrintVariables(){
        foreach(var scope in _scopes.Keys){
            _logger.LogCritical($"SCOPE: ------------------{scope}--------------------");
            foreach(var variable in _scopes[scope]){
                _logger.LogWarning($"VARIABLE: {variable.Name} ---------- VALUE: {variable.Value}");
            }
        }
    }
    public void Debug(){
        _logger.LogInformation("Starting debugging");
        bool nextStep = false;
        foreach(var line in _variableFetcher.Lines){
            if(_breakPoints.BreakPoints.ContainsKey(line.Item2) || nextStep){
                _logger.LogCritical($"Break point found:\n{line.Item1.Trim()}\n{DEFAULT_COMMANDS}");
                int command = 0;
                try{
                    command = Convert.ToInt32(Console.ReadLine() ?? "0");
                }catch{
                    command = 0;
                }
                //PrintVariables();
                switch(command){
                    case 1:
                        _logger.LogInformation("Continue instruction...");
                        nextStep = false;
                    break;
                    case 2:
                        _logger.LogInformation("Next instruction set");
                        nextStep = true;
                    break;
                    case 3:
                        _logger.LogInformation("Step into instruction...");
                        nextStep = false;
                    break;
                    default:
                        _logger.LogInformation("Default (continue)");
                        break;
                }
            }
            ExecuteTask(line.Item1);
        }
    }
    private void ExecuteTask(string line){
        if(line.Contains("<=")){
            var splitted = line.Split("<=", 2);
            string assignation = splitted[0].Trim();
            var items = splitted[1].Split(" ");
            if(!_scopes[_currentScope].Contains(assignation))
                throw new ParserException(Constants.ParseExceptions.MISSING_DECLARATION);
            
        }else if(line.Contains("architecture") && line.Contains("of")){
            var items = line.Split("of", 2);
            _currentScope = items[1].Split(" ")[0].Trim();
        }
    }
    private object EvaluateOperation(string[] items){
        List<Variable> Variables = new();
        foreach(var item in items){
            var temp = item.Trim();
            if(_scopes[_currentScope].Contains(temp)){
                Variables.Add(_scopes[_currentScope].First(p => temp == p));
            }else{
                
            }
        }
        return new();
    }
}