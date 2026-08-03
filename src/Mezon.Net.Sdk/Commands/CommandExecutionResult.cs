namespace Mezon.Net.Sdk.Commands
{
    public enum CommandExecutionResult
    {
        NotACommand,
        UnknownCommand,
        OnCooldown,
        Unauthorized,
        Executed,
        Failed,
    }
}
