param([string]$Exe=(Join-Path $PSScriptRoot '../out/native-widget/Podglow.exe'))
$ErrorActionPreference='Stop'
Add-Type @'
using System;using System.Runtime.InteropServices;using System.IO.MemoryMappedFiles;
public class CompactAirPodsTest {
 [DllImport("user32.dll")]public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [StructLayout(LayoutKind.Sequential)]public struct Point{public int X,Y;}
 [DllImport("user32.dll")]static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")]static extern bool ClientToScreen(IntPtr h,ref Point p);
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]static extern void mouse_event(uint f,uint x,uint y,uint data,UIntPtr extra);
 static Point originalPointer;static bool pointerSaved;
 public static void Wheel(IntPtr h,IntPtr point,int delta){if(!pointerSaved){GetCursorPos(out originalPointer);pointerSaved=true;}long packed=point.ToInt64();var p=new Point{X=(short)(packed&65535),Y=(short)((packed>>16)&65535)};ClientToScreen(h,ref p);SetForegroundWindow(h);SetCursorPos(p.X,p.Y);System.Threading.Thread.Sleep(80);mouse_event(0x0800,0,0,unchecked((uint)delta),UIntPtr.Zero);}
 public static void RestorePointer(){if(pointerSaved){SetCursorPos(originalPointer.X,originalPointer.Y);pointerSaved=false;}}
 [DllImport("user32.dll")]public static extern bool SetProcessDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll")]public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string c,string t);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="FindWindowW")]public static extern IntPtr FindTitle(IntPtr c,string t);
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr GetProp(IntPtr h,string n);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
 public static int SharedInt(int offset){using var m=MemoryMappedFile.OpenExisting(@"Local\IslandAirPods.v1",MemoryMappedFileRights.Read);using var v=m.CreateViewAccessor(0,688,MemoryMappedFileAccess.Read);return v.ReadInt32(offset);}
}
'@
[CompactAirPodsTest]::SetProcessDpiAwarenessContext([IntPtr](-4))|Out-Null
[CompactAirPodsTest]::SetThreadDpiAwarenessContext([IntPtr](-4))|Out-Null
$status=Join-Path $env:LOCALAPPDATA 'AirPodsIsland\status.json'
function State { [pscustomobject]@{connected=$true} }
function Wait-State($property,$value){for($i=0;$i -lt 75;$i++){Start-Sleep -Milliseconds 150;if((State).$property -eq $value){Start-Sleep -Milliseconds 350;return}};throw "State $property did not become $value"}
$process=Start-Process -FilePath ([IO.Path]::GetFullPath($Exe)) -ArgumentList '--preview' -PassThru
Start-Sleep -Milliseconds 900
$island=[CompactAirPodsTest]::FindWindow('AirPodsCompanionWidget','Podglow — Preview')
if($island -eq [IntPtr]::Zero){throw 'Native standalone widget missing'}
$panel=$island
function Page-Check($page,$height){Start-Sleep -Milliseconds 1300;if([CompactAirPodsTest]::GetProp($panel,'AirPodsPage').ToInt64() -ne $page){throw ('Wrong widget page: wanted='+$page+' actual='+[CompactAirPodsTest]::GetProp($panel,'AirPodsPage').ToInt64()+' visible='+[CompactAirPodsTest]::IsWindowVisible($panel)+' height='+[CompactAirPodsTest]::GetProp($panel,'AirPodsContentHeight').ToInt64())};if([CompactAirPodsTest]::GetProp($panel,'AirPodsContentHeight').ToInt64() -ne $height){throw 'Wrong content height'}}
function Position($x,$y){$scale=[CompactAirPodsTest]::GetProp($panel,'AirPodsScale1000').ToInt64()/1000.0;if($scale -le 0){throw 'Invalid widget scale'};$xx=[int][Math]::Round($x*$scale)+[CompactAirPodsTest]::GetProp($panel,'AirPodsOffsetX').ToInt64();$yy=[int][Math]::Round($y*$scale)+[CompactAirPodsTest]::GetProp($panel,'AirPodsOffsetY').ToInt64();[IntPtr](($yy -shl 16) -bor ($xx -band 65535))}
function Click($x,$y){[CompactAirPodsTest]::SetForegroundWindow($panel)|Out-Null;Start-Sleep -Milliseconds 120;$point=Position $x $y;[CompactAirPodsTest]::SendMessage($panel,0x200,[IntPtr]::Zero,$point)|Out-Null;[CompactAirPodsTest]::SendMessage($panel,0x201,[IntPtr]1,$point)|Out-Null;Start-Sleep -Milliseconds 120;$point=Position $x $y;[CompactAirPodsTest]::SendMessage($panel,0x202,[IntPtr]::Zero,$point)|Out-Null}
function Prop($name){[CompactAirPodsTest]::GetProp($panel,$name).ToInt64()}
function Wait-Prop($name,$wanted){for($i=0;$i -lt 40;$i++){Start-Sleep -Milliseconds 75;if((Prop $name) -eq $wanted){Start-Sleep -Milliseconds 250;return}};throw "Widget property $name did not become $wanted"}
function Wait-Link($up){for($i=0;$i -lt 90;$i++){Start-Sleep -Milliseconds 400;$s=State;if($s.connected -eq $up -and (!$up -or $s.backend -eq 2)){Start-Sleep -Seconds 2;return}};throw "AirPods connection did not become $up"}
try{
 Page-Check 0 454
 Click 352 209
 if((State).connected){Wait-Prop 'AirPodsHelpId' 71;Click 100 325;Wait-Prop 'AirPodsHelpId' 0}
 Click 325 130;Wait-Prop 'AirPodsHelpId' 93;Click 100 325;Wait-Prop 'AirPodsHelpId' 0
 Click 318 32;Page-Check 1 538
 Click 294 408;Wait-Prop 'AirPodsHelpId' 76;Click 338 408;Wait-Prop 'AirPodsHelpId' 0
 Click 160 477;Page-Check 1 738
 Click 160 630;Page-Check 2 620
 foreach($help in @(@(350,104,78),@(350,238,79),@(350,375,80),@(294,504,81))){Click $help[0] $help[1];Wait-Prop 'AirPodsHelpId' $help[2];Click 100 350;Wait-Prop 'AirPodsHelpId' 0}
 Click 318 32;Page-Check 1 738;Click 160 477;Page-Check 1 538;Click 318 32;Page-Check 0 454
 'PASS shared native renderer: main/settings/help/expand/collapse/back navigation'
 [CompactAirPodsTest]::PostMessage($panel,0x80c2,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;Start-Sleep -Milliseconds 300
 $credits=[CompactAirPodsTest]::FindTitle([IntPtr]::Zero,'Podglow — Credits & licenses');if($credits -eq [IntPtr]::Zero){throw 'Native credits window missing'}
 [CompactAirPodsTest]::PostMessage($credits,0x10,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
 'PASS credits reachable from standalone adapter; no Bluetooth writes or screen recording'
}finally{[CompactAirPodsTest]::PostMessage($panel,0x80c3,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;if(!$process.WaitForExit(3000)){$process.Kill()}}