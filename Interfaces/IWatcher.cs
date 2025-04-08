using Parser.Models;
namespace Debugger.Interfaces;
public interface IWatcher {
    void Watch(Variable variable);
}