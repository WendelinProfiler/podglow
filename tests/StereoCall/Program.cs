using WPods.Core;
static void Check(bool value) { if (!value) throw new Exception("Stereo call transport invariant failed"); }
Native.Reset(true); HandsFree.SetEnabled(1, false); Check(string.Join(",",Native.Calls)=="disconnect,disable,connect,close");
Native.Reset(false); HandsFree.SetEnabled(1, false); Check(string.Join(",",Native.Calls)=="close");
Native.Reset(false); HandsFree.SetEnabled(1, true); Check(string.Join(",",Native.Calls)=="enable,close");
Native.Reset(true); Native.FailChange=true; try { HandsFree.SetEnabled(1,false); throw new Exception("Expected failure"); } catch(System.ComponentModel.Win32Exception) {} Check(string.Join(",",Native.Calls)=="disconnect,disable,connect,close");
Native.Reset(true); Native.FailDisconnect=true; try { HandsFree.SetEnabled(1,false); throw new Exception("Expected failure"); } catch(System.ComponentModel.Win32Exception) {} Check(string.Join(",",Native.Calls)=="disconnect,close");
Console.WriteLine("PASS: transport rebuild, already-prepared connection, legacy HFP, failed change recovery, failed release isolation");
namespace WPods.Core {
 internal static class Log { public static void Warn(string a,string b) {} }
 internal static unsafe class Native {
  internal const uint ErrorSuccess=0, ErrorMoreData=234, ErrorNotFound=1168, ErrorInvalidParameter=87, EInvalidArg=0x80070057;
  internal struct BluetoothDeviceInfo {}
  internal static bool Enabled,FailChange,FailDisconnect; internal static List<string> Calls=new();
  internal static void Reset(bool enabled){Enabled=enabled;FailChange=FailDisconnect=false;Calls.Clear();}
  internal static bool TryFindPairedDevice(ulong a,out nint r,out BluetoothDeviceInfo d){r=1;d=new();return true;}
  internal static void CloseHandle(nint r){Calls.Add("close");}
  internal static int wpn_bt_disconnect_audio(ulong a){Calls.Add("disconnect");return FailDisconnect?unchecked((int)0x80004005):0;}
  internal static uint BluetoothSetServiceState(nint r,BluetoothDeviceInfo d,Guid s,uint state){Calls.Add(state==0?"disable":"enable");if(FailChange)return 5;Enabled=state!=0;return 0;}
  internal static uint BluetoothEnumerateInstalledServices(nint r,in BluetoothDeviceInfo d,ref uint n,Guid* s){if(!Enabled){n=0;return 0;}n=1;if(s==null)return ErrorMoreData;s[0]=new("0000111E-0000-1000-8000-00805F9B34FB");return 0;}
 }
}
namespace WPods.Nearby { internal static class NearbyNative {internal static int wpn_bt_connect_audio(ulong a){Native.Calls.Add("connect");return 0;}} }
