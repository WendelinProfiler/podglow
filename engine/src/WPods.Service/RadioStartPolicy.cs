namespace WPods.Service;

internal enum RadioStartState { Missing, Starting, Ready, Failed, Disabled }

internal static class RadioStartPolicy
{
    internal static RadioStartState Classify(bool present, uint status, uint problem)
    {
        if (!present) return RadioStartState.Missing;
        if (problem == 22) return RadioStartState.Disabled;
        if (problem != 0) return RadioStartState.Failed;
        return (status & 8 /* DN_STARTED */) != 0 ? RadioStartState.Ready : RadioStartState.Starting;
    }
}
