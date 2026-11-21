param([Parameter(Mandatory)][string]$Payload)
$ErrorActionPreference='Stop'
$privateRoot=[regex]::Escape([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')))
foreach($name in @('AirPodsCompanion.exe','IslandAirPodsHost.exe','WPods.Service.exe','AirPodsAudio.dll','wpods_native.dll','hostfxr.dll','coreclr.dll','System.Windows.Forms.dll','locales/en-US.json','LICENSE','CREDITS.md','THIRD-PARTY-NOTICES.txt','licenses/WPods-THIRD-PARTY-NOTICES.txt')){
 if(!(Test-Path -LiteralPath (Join-Path $Payload $name))){throw "Missing runtime file: $name"}
}
$files=Get-ChildItem -LiteralPath $Payload -Recurse -File
foreach($file in $files){
 if($file.Name -match '(?i)(\.pfx$|\.p12$|\.key$|\.pdb$|\.log$|^settings\.ini$|^status\.json$|battery-cache|evidence\.json|LocalGlyphs|AirPodsOriginalGlyphs)'){throw "Private or build-only file in payload: $($file.Name)"}
 $bytes=[IO.File]::ReadAllBytes($file.FullName)
 foreach($encoding in @([Text.Encoding]::UTF8,[Text.Encoding]::Unicode)){
  $text=$encoding.GetString($bytes)
  if($text -match $privateRoot -or $text -match '(?i)(github_pat_[A-Za-z0-9_]{20}|gh[pousr]_[A-Za-z0-9]{20})'){throw "Private build path or credential pattern in: $($file.Name)"}
 }
}
"Payload file checks passed ($($files.Count) files). No installation or hardware test performed."
