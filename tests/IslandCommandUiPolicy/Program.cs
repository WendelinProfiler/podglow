using WPods.Ui;

foreach (int command in Enumerable.Range(0, 18))
{
    bool expected = command is 1 or 2;
    if (IslandCommandUiPolicy.ConnectionBusy(true, command) != expected)
        throw new Exception($"Wrong connection activity for command {command}");
    if (IslandCommandUiPolicy.ConnectionBusy(false, command))
        throw new Exception($"Idle command {command} still shows connection activity");
}
// A volume drag repeatedly enters/leaves the command guard. Neither transition
// may change the connection button, while a real reconnect must remain visible.
foreach (bool busy in new[] { false, true, false, true, false })
    if (IslandCommandUiPolicy.ConnectionBusy(busy, 12))
        throw new Exception("Volume changes leaked into the connection indicator");
Console.WriteLine("PASS: 36 command/idle cases and repeated volume transitions");
