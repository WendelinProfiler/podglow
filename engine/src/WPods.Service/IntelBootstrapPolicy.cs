namespace WPods.Service;

internal static class IntelBootstrapPolicy
{
    internal static bool Required(string instance, bool completedThisBoot) =>
        !completedThisBoot && instance.StartsWith(@"USB\VID_8087&", StringComparison.OrdinalIgnoreCase);
}
