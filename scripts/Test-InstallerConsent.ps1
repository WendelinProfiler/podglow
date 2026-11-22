param([Parameter(Mandatory)][string]$Compiler)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$source=Get-Content -Raw (Join-Path $root 'installer/Companion.iss')
$wizard=[regex]::Match($source,'(?s)procedure InitializeWizard;.*?(?=// The Bluetooth adapter page)').Value
$next=[regex]::Match($source,'(?s)function NextButtonClick\(.*?(?=function InitializeUninstall)').Value
if(!$wizard -or !$next){throw 'Production wizard event functions not found.'}
$out=Join-Path $root ('out/consent-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $out|Out-Null
$fixture=@"
[Setup]
AppName=Podglow consent probe
AppVersion=0.0.0
DefaultDirName={tmp}\PodglowConsentProbe
PrivilegesRequired=lowest
Uninstallable=no
CreateAppDir=no
OutputDir=$out
OutputBaseFilename=ConsentProbe
[Code]
var DownloadPage: TDownloadWizardPage; BluetoothPage: TOutputMsgWizardPage; BluetoothConsent: TNewCheckBox;
$wizard
$next
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssDone then SaveStringToFile(ExpandConstant('{param:RESULTFILE|}'), 'consent passed', False);
end;
"@
$file=Join-Path $out 'Probe.iss';[IO.File]::WriteAllText($file,$fixture,[Text.UTF8Encoding]::new($false))
& $Compiler /Qp $file
if($LASTEXITCODE){throw 'Consent-only probe compilation failed.'}
foreach($accept in @('0','1')){
 $marker=Join-Path $out ('result-'+$accept+'.txt')
 $info=[Diagnostics.ProcessStartInfo]::new((Join-Path $out 'ConsentProbe.exe'));$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 foreach($arg in @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/ACCEPTBLUETOOTH='+$accept),('/RESULTFILE='+$marker))){$info.ArgumentList.Add($arg)}
 $p=[Diagnostics.Process]::Start($info)
 if(!$p.WaitForExit(15000)){$p.Kill();throw 'Consent-only probe blocked unexpectedly.'}
 if($accept -eq '1' -and ($p.ExitCode -ne 0 -or !(Test-Path $marker))){throw 'Explicit silent consent did not complete.'}
 if($accept -eq '0' -and (Test-Path $marker)){throw 'Silent setup proceeded without consent.'}
 "PASS production wizard events with explicit consent=$accept; no drivers, services or app files installed."
}
