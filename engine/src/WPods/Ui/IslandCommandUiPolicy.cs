namespace WPods.Ui;

internal static class IslandCommandUiPolicy
{
    // The connection button must not show activity for volume or listening-mode commands.
    // The command handler keeps its separate serialization guard for every command.
    internal static bool ConnectionBusy(bool commandInFlight, int command) =>
        commandInFlight && command is 1 or 2;
}
