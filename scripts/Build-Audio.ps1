param([string]$OutputDirectory=(Join-Path $PSScriptRoot '../out/engine'))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$source=Join-Path $root 'src/Audio/AirPodsAudio.cpp'
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if(!(Test-Path -LiteralPath $vswhere)){throw 'Visual Studio Build Tools with the C++ workload and Windows SDK are required.'}
$installation=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$installation){throw 'The MSVC x64 toolchain was not found.'}
$vars=Join-Path $installation 'VC/Auxiliary/Build/vcvars64.bat'
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$output=[IO.Path]::GetFullPath($OutputDirectory)
$script=Join-Path $output 'compile-audio.cmd'
$commands=@('@echo off','chcp 65001 >nul',('call "'+$vars+'" >nul'),('cl /nologo /std:c++20 /EHsc /O2 /MT /LD "'+$source+'" /Fo:"'+(Join-Path $output 'AirPodsAudio.obj')+'" /Fe:"'+(Join-Path $output 'AirPodsAudio.dll')+'" /link ole32.lib uuid.lib propsys.lib'),'exit /b %errorlevel%')
[IO.File]::WriteAllText($script,($commands -join "`r`n"),[Text.UTF8Encoding]::new($false))
& $env:ComSpec /d /c $script
if($LASTEXITCODE -ne 0){throw 'Native audio helper build failed.'}
