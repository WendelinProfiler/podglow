using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WPods.Service;

// Publish only a validated report from the configured direct mouse into its
// existing owner's profile. No device keys, pairing or user preferences change.
internal static class MouseBatteryPublication
{
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WritePrivateProfileString(string section,string key,string value,string file);

    internal static void Publish(ulong address,int percent)
    {
        if(percent is <0 or >100)return;
        try {
            using var config=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GlideMouse\Handoff");
            if(config?.GetValue("DirectHidTarget") is not long target || unchecked((ulong)target)!=address || config.GetValue("DirectOwnerSid") is not string sid)return;
            using var profile=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\"+sid);
            if(profile?.GetValue("ProfileImagePath") is not string root)return;
            var file=Path.Combine(Environment.ExpandEnvironmentVariables(root),"AppData","Local","IslandMagicMouse","settings.ini");
            if(!File.Exists(file))return;
            bool mouse=false,matched=false;
            foreach(var raw in File.ReadLines(file)) {
                var line=raw.Trim();
                if(line.StartsWith('[')){mouse=line.Equals("[Mouse]",StringComparison.OrdinalIgnoreCase);continue;}
                if(mouse && line.StartsWith("Address=",StringComparison.OrdinalIgnoreCase) && ulong.TryParse(line[8..],System.Globalization.NumberStyles.HexNumber,null,out var selected) && selected==address){matched=true;break;}
            }
            if(!matched)return;
            if(!WritePrivateProfileString("Mouse","Battery",percent.ToString(System.Globalization.CultureInfo.InvariantCulture),file))return;
            if(!WritePrivateProfileString("Mouse","BatteryTime",DateTime.UtcNow.ToFileTimeUtc().ToString(System.Globalization.CultureInfo.InvariantCulture),file))return;
            WritePrivateProfileString("Mouse","BatteryRefreshResult","direct-hid",file);
            Log.Info("mouse",$"direct HID battery received: {percent}%");
        } catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
            Log.Info("mouse","direct HID battery publication unavailable");
        }
    }
}
