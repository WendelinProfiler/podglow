using WPods.Service;
var cases = new (int exit, string output, string error, bool expected)[]
{
    (0, "", "", true),
    (0, "\r\n", "", true),
    (0, "Imported USB devices\r\nPort 01: device in use\r\n -> usbip://127.0.0.1:1234/1-1", "", false),
    (0, "Imported USB devices", "", false),
    (1, "", "", false),
    (0, "", "Access denied", false),
    (0, "Unknown command", "", false),
};
foreach (var c in cases)
    if (UsbImportPolicy.IsEmpty(c.exit, c.output, c.error) != c.expected)
        throw new Exception($"Unsafe import-list classification: {c}");
Console.WriteLine($"PASS {cases.Length} import-list cases; successful empty output accepted, active/unknown/failed results refused.");
