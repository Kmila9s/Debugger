using Interpreter.Models;
using Interpreter.Services;
using Parser.Repositories;

namespace Debugger.Repositories;

public class Parser
{
    public string FileContent { get; private set; }
    public List<Entity> Entities { get; private set; } = new();
    private List<string> _excludedEnds = new()
    {
        "if", "when", "process"
    };
    public Parser(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException();
        FileContent = File.ReadAllText(filePath);
    }
    public void ParseFile()
    {
        Tokenizer tokenizer = new("");
        string[] content = FileContent.Split("\n")
                            .Select(p => p.Trim())
                            .Where(p => !String.IsNullOrEmpty(p) && p != " ")
                            .ToArray();
        List<string> rawEntity = new();
        int it = 0;
        string currentEntityArchName = string.Empty;
        foreach(var item in content){
            string comparer = item.Trim().ToLower();
            if(comparer.StartsWith("entity")){
                it = 1;
            }else if(comparer.StartsWith("architecture")){
                currentEntityArchName = item.Split(' ')[3];
                it = 2;
            }
            if(it == 1){
                if(comparer.StartsWith("end ") && comparer.EndsWith(";") && !_excludedEnds.Any(comparer.EndsWith)){
                    rawEntity.Add(item.Trim());
                    Entity entity = new(rawEntity.ToArray());
                    Entities.Add(entity);
                    rawEntity = new();
                    it = 0;
                }else
                    rawEntity.Add(item);
            }else if(it == 2){
                string clean = comparer.TrimEnd(';');
                if (comparer.StartsWith("end ") && !_excludedEnds.Any(clean.EndsWith))
                {
                    rawEntity.Add(item);
                    Entity entity = Entities.FirstOrDefault(p => p.Name.Equals(currentEntityArchName)) ?? default!;
                    if (entity == null) throw new Exception("Entity not found");
                    Architecture arch = new(entity.Ports, entity.Generics);
                    arch.Syntetize(rawEntity.ToArray());
                    entity.SetArchitecture(arch);
                    it = 0;
                }
                else
                    rawEntity.Add(item);
            }
        }
    }
}