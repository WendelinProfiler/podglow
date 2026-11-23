namespace WPods.Service;

internal static class UsbImportPolicy
{
    // usbip 0.9.8.1 emits nothing (not even the heading) when no imports exist.
    // Only that successful, empty result proves the controller is unused. Any
    // unexpected text, stderr or failed query must refuse recovery.
    internal static bool IsEmpty(int exitCode, string output, string errors) =>
        exitCode == 0 && string.IsNullOrWhiteSpace(output) && string.IsNullOrWhiteSpace(errors);
}
