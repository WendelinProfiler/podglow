using WPods.Service;
var cases = new (string id, bool completed, bool required)[]
{
    (@"USB\VID_8087&PID_0033\physical", false, true),
    (@"usb\vid_8087&pid_0033\physical", false, true),
    (@"USB\VID_8087&PID_0033\physical", true, false),
    (@"USB\VID_0BDA&PID_1234\physical", false, false),
    (@"ROOT\USB\0000", false, false),
};
foreach (var c in cases)
    if (IntelBootstrapPolicy.Required(c.id, c.completed) != c.required)
        throw new Exception($"Incorrect firmware-bootstrap scope: {c}");
Console.WriteLine($"PASS {cases.Length} Intel bootstrap cases; once per boot, Intel-only, no generic USB controller takeover.");
