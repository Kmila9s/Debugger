using Debugger.Interfaces;
using Parser.Models;
using Microsoft.Extensions.Logging;

namespace Debugger.Repositories;
public class PortWatcher : IWatcher {
    private readonly ILogger<PortWatcher> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PortWatcher>();
    public List<Parser.Models.Variable> Variables { get; private set; } = new();
    public void Watch(Variable variable) => throw new NotImplementedException();
}