namespace dnd_game.Domain.Commands
{
    public class TriggerScriptCommand(string scriptName, Dictionary<string, object> parameters) : ICommand
    {
        public string ScriptName { get; } = scriptName;
        public Dictionary<string, object> Parameters { get; } = parameters;
    }
}