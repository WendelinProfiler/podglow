param(
 [string]$Version='0.1.9',
 [string]$ReleaseTag,
 [string]$DotNet='dotnet',
 [Parameter(Mandatory)][string]$InnoUnpacker,
 [Parameter(Mandatory)][string]$InnoCompiler
)
$ErrorActionPreference='Stop'
if(!$ReleaseTag){$ReleaseTag="v$Version-alpha.1"}
if($ReleaseTag -notmatch ('^v'+[regex]::Escape($Version)+'(?:-[0-9A-Za-z.-]+)?$')){throw 'ReleaseTag must match the packaged version.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output=Join-Path $root 'out'
$deps=Join-Path $output 'deps'
New-Item -ItemType Directory -Force $deps | Out-Null
. (Join-Path $root 'engine/native/common.ps1')
$upstream=Join-Path $deps 'WPods-Setup-0.4.2.exe'
Get-Pinned 'https://github.com/FPNDev/wpods/releases/download/v0.4.2/WPods-Setup.exe' $upstream '00DBDB220681931ADDC1AB6E0D90F5760AB0E3377099F6E10320C25AA76E5801'
$extracted=Join-Path $deps 'wpods'
if(!(Test-Path (Join-Path $extracted '{app}/wpods_native.dll'))){
 & $InnoUnpacker -x -q -m "-d$extracted" $upstream
 if($LASTEXITCODE){throw 'Upstream dependency extraction failed.'}
}
$original=Join-Path $extracted '{app}'
$native=Join-Path $root 'engine/.build/native/bin'
New-Item -ItemType Directory -Force $native | Out-Null
foreach($name in @('wpods_native.dll','avcodec-61.dll','avutil-59.dll','swresample-5.dll','rnnoise.dll','deepfilter.dll','webrtc_apm.dll','onnxruntime.dll','DeepFilterNet3_onnx.tar.gz')){
 Copy-Item -LiteralPath (Join-Path $original $name) -Destination $native -Force
}
Copy-Item -LiteralPath (Join-Path $original 'models') -Destination $native -Recurse -Force
Copy-Item -LiteralPath (Join-Path $original 'web') -Destination (Join-Path $root 'engine/src/WPods') -Recurse -Force
& (Join-Path $PSScriptRoot 'Build-NativeWidget.ps1')
& (Join-Path $PSScriptRoot 'Build-Audio.ps1')
$payload=Join-Path $output ("release-$Version/payload")
if((Test-Path $payload) -and @(Get-ChildItem -LiteralPath $payload -Force).Count -gt 0){throw 'Release payload already exists. Use a new version or move the old build aside before rebuilding.'}
New-Item -ItemType Directory -Force $payload | Out-Null
@{Version=$ReleaseTag} | ConvertTo-Json | Set-Content (Join-Path $payload 'companion-release.json') -Encoding utf8
foreach($project in @('WPods','WPods.Service')){
 & $DotNet publish (Join-Path $root "engine/src/$project/$project.csproj") -c Release -r win-x64 --self-contained true -o $payload "-p:Version=$Version" '-p:DebugType=None' '-p:DebugSymbols=false' '-p:PublishReadyToRun=false' "-p:PathMap=$root=/_/source" '-p:ContinuousIntegrationBuild=true'
 if($LASTEXITCODE){throw "$project publish failed."}
}
Copy-Item -LiteralPath (Join-Path $output 'native-widget/Podglow.exe') -Destination $payload
Copy-Item -LiteralPath (Join-Path $output 'engine/AirPodsAudio.dll') -Destination $payload
foreach($name in @('LICENSE','CREDITS.md','RELEASE-NOTES.md','RELEASE-CHECKLIST.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $payload}
New-Item -ItemType Directory -Force (Join-Path $payload 'docs') | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'docs/UPDATES.md'),(Join-Path $root 'docs/BLUETOOTH-RECOVERY.md'),(Join-Path $root 'docs/BLUETOOTH-STARTUP.md') -Destination (Join-Path $payload 'docs')
New-Item -ItemType Directory -Force (Join-Path $payload 'support') | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Repair-Bluetooth.ps1') -Destination (Join-Path $payload 'support/Repair-Bluetooth.ps1')
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $payload -Recurse
New-Item -ItemType Directory -Force (Join-Path $payload 'drivers') | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'engine/installer/drivers.ps1') -Destination (Join-Path $payload 'drivers/drivers.ps1')
$usbip=Join-Path $deps 'USBip-0.9.8.1-x64.exe'
Copy-Item -LiteralPath (Join-Path $extracted '{tmp}/USBip-0.9.8.1-x64.exe') -Destination $usbip -Force
if((Get-FileHash $usbip).Hash -ne '38CAD6D4432B52D5BB9409D9AD03B72FDFFC4ADA4CD3A48FBECA1A2752A8518A'){throw 'usbip-win2 hash mismatch.'}
& (Join-Path $PSScriptRoot 'Test-ReleasePayload.ps1') -Payload $payload
& $InnoCompiler '/Qp' "/DAppVersion=$Version" "/DPayload=$payload" "/DUsbipFile=$usbip" "/O$(Join-Path $output "release-$Version")" (Join-Path $root 'installer/Companion.iss')
if($LASTEXITCODE){throw 'Installer compilation failed.'}
$manifest=Get-ChildItem $payload -File -Recurse | Sort-Object FullName | ForEach-Object {
 [ordered]@{Path=[IO.Path]::GetRelativePath($payload,$_.FullName).Replace('\','/');Bytes=$_.Length;SHA256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()}
}
$manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path (Split-Path $payload) 'payload-manifest.json') -Encoding utf8
Get-FileHash (Join-Path (Split-Path $payload) 'Podglow-Setup.exe') | ForEach-Object { $_.Hash.ToLowerInvariant()+'  Podglow-Setup.exe' } | Set-Content (Join-Path (Split-Path $payload) 'SHA256SUMS.txt') -Encoding ascii
'Experimental installer built. It has not been installed or tested on clean Windows.'
