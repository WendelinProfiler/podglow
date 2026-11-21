param([string]$File='../out/shared-widget-preview.png',[ValidateRange(0,2)][int]$Page=0,[switch]$Expand)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;using System.Runtime.InteropServices;
public class AirPodsRenderTest {
 [StructLayout(LayoutKind.Sequential)]public struct Rect{public int L,T,R,B;}
 [DllImport("user32.dll")]public static extern bool SetProcessDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll")]public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string c,string t);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint f);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr GetProp(IntPtr h,string n);
 [DllImport("user32.dll")]public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
}
'@
[AirPodsRenderTest]::SetProcessDpiAwarenessContext([IntPtr](-4))|Out-Null
[AirPodsRenderTest]::SetThreadDpiAwarenessContext([IntPtr](-4))|Out-Null
$h=[AirPodsRenderTest]::FindWindow('AirPodsCompanionWidget','AirPods Companion — Preview')
if($h -eq [IntPtr]::Zero -or ![AirPodsRenderTest]::IsWindowVisible($h)){$island=[AirPodsRenderTest]::FindWindow('AirPodsCompanionWidget','AirPods Companion — Preview');[AirPodsRenderTest]::PostMessage($island,0x80c1,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;Start-Sleep -Milliseconds 1000;$h=[AirPodsRenderTest]::FindWindow('AirPodsCompanionWidget','AirPods Companion — Preview')}
function Navigate-Click($x,$y){$scale=[AirPodsRenderTest]::GetProp($h,'AirPodsScale1000').ToInt64()/1000.0;$xx=[int]($x*$scale)+[AirPodsRenderTest]::GetProp($h,'AirPodsOffsetX').ToInt64();$yy=[int]($y*$scale)+[AirPodsRenderTest]::GetProp($h,'AirPodsOffsetY').ToInt64();$point=[IntPtr](($yy -shl 16)-bor($xx-band65535));[AirPodsRenderTest]::SendMessage($h,0x201,[IntPtr]1,$point)|Out-Null;[AirPodsRenderTest]::SendMessage($h,0x202,[IntPtr]::Zero,$point)|Out-Null;Start-Sleep -Milliseconds 900}
$current=[AirPodsRenderTest]::GetProp($h,'AirPodsPage').ToInt64()
while($current -gt 0){Navigate-Click 318 32;$current=[AirPodsRenderTest]::GetProp($h,'AirPodsPage').ToInt64()}
if($Page -ge 1){Navigate-Click 318 32}
if($Page -eq 2 -or $Expand){if([AirPodsRenderTest]::GetProp($h,'AirPodsMoreExpanded').ToInt64() -eq 0){Navigate-Click 160 477}}
if($Page -eq 2){Navigate-Click 160 630}
$r=[AirPodsRenderTest+Rect]::new();[AirPodsRenderTest]::GetWindowRect($h,[ref]$r)|Out-Null
if($r.R -le $r.L -or $r.B -le $r.T){throw 'No widget surface'}
$bitmap=New-Object Drawing.Bitmap ($r.R-$r.L),($r.B-$r.T)
$graphics=[Drawing.Graphics]::FromImage($bitmap);$dc=$graphics.GetHdc()
try{if(![AirPodsRenderTest]::PrintWindow($h,$dc,2)){throw 'Own-window PrintWindow unavailable'}}finally{$graphics.ReleaseHdc($dc);$graphics.Dispose()}
try{$bitmap.Save((Join-Path $PSScriptRoot $File),[Drawing.Imaging.ImageFormat]::Png)}finally{$bitmap.Dispose()}
'Saved own widget window surface only. No desktop capture, WGC, video or yellow border.'
