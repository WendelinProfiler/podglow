param([switch]$CheckOnly)
$ErrorActionPreference='Stop'

# Match both the USB/IP hardware ID and driver service. Never select a
# physical USB host controller or hub by its display name or instance number.
$controllers=@(Get-PnpDevice -PresentOnly -Class USB | Where-Object {
 $ids=@((Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName DEVPKEY_Device_HardwareIds -ErrorAction SilentlyContinue).Data)
 $driver=(Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName DEVPKEY_Device_Service -ErrorAction SilentlyContinue).Data
 $ids -contains 'ROOT\USBIP_WIN2\UDE' -and $driver -eq 'usbip2_ude'
})
if($controllers.Count -ne 1){throw 'Expected exactly one usbip-win2 virtual controller. Nothing was changed.'}
$service=Get-CimInstance Win32_Service -Filter "Name='WPodsService'"
if(!$service -or $service.PathName -notmatch '^\s*"([^\"]+\\WPods\.Service\.exe)"\s*$'){throw 'A supported Podglow service installation was not found.'}
$engine=$Matches[1]
$folder=Split-Path -LiteralPath $engine
if(!(Test-Path -LiteralPath (Join-Path $folder 'Podglow.exe')) -or !(Test-Path -LiteralPath (Join-Path $folder 'companion-release.json'))){throw 'The service does not belong to a Podglow installation. Nothing was changed.'}
if($CheckOnly){'Podglow and one usbip-win2 virtual controller found. No service or device was changed.';return}
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$principal=[Security.Principal.WindowsPrincipal]::new($identity)
if(!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Open Windows PowerShell as administrator and run this script again.'}
Write-Output 'Reconnecting the virtual Bluetooth adapter and Podglow microphone. Bluetooth devices may briefly disconnect.'
try{
 & $engine --stop
 if($LASTEXITCODE -ne 0){throw 'Podglow did not stop safely. The controller was not reset.'}
 & (Join-Path $env:SystemRoot 'System32/pnputil.exe') /restart-device $controllers[0].InstanceId
 if($LASTEXITCODE -ne 0){throw "Windows returned $LASTEXITCODE while restarting the virtual controller. No Windows restart was requested."}
}finally{Start-Service -Name WPodsService}
for($i=0;$i -lt 40;$i++){
 Start-Sleep -Milliseconds 500
 if(@(Get-PnpDevice -PresentOnly -Class Bluetooth -ErrorAction SilentlyContinue|Where-Object {$_.InstanceId -like 'USB\*' -and $_.Status -eq 'OK'}).Count){'Bluetooth adapter is available again. Open the AirPods case to reconnect.';return}
}
throw 'The adapter is still unavailable. Keep the service logs and report the problem. Windows was not restarted.'
