using WPods.Service;
var cases = new (bool present, uint flags, uint problem, RadioStartState expected)[]
{
    (false, 8, 0, RadioStartState.Missing),
    (true, 0, 0, RadioStartState.Starting),
    (true, 8, 0, RadioStartState.Ready),
    (true, 8, 10, RadioStartState.Failed),
    (true, 0, 10, RadioStartState.Failed),
    (true, 8, 22, RadioStartState.Disabled),
    (true, 8, 43, RadioStartState.Failed),
};
foreach (var c in cases)
    if (RadioStartPolicy.Classify(c.present, c.flags, c.problem) != c.expected)
        throw new Exception($"Incorrect PnP readiness: {c}");
Console.WriteLine($"PASS {cases.Length} PnP readiness cases, including Code 10 with a stale started flag and explicit user disable.");
