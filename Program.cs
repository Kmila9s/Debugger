
using System.Runtime.InteropServices;
using Debugger.Repositories;

namespace Debugger;

class Program
{
    static void Main(string[] args)
    {
        var files = Directory.GetFiles("Codes");
        foreach (var file in files)
        {
            Console.WriteLine(file);
            Repositories.Parser parser = new(file);
            parser.ParseFile();
            Console.WriteLine($"Entities found: {parser.Entities.Count}");    
        }
        
    }
}