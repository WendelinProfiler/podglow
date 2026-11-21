param(
 [string]$OutputDirectory=(Join-Path $PSScriptRoot '../out/native-widget'),
 [string]$LocalGlyphHeader
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if(!(Test-Path -LiteralPath $vswhere)){throw 'Visual Studio Build Tools with MSVC and the Windows SDK are required.'}
$installation=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$installation){throw 'MSVC x64 was not found.'}
$vars=Join-Path $installation 'VC/Auxiliary/Build/vcvars64.bat'
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$output=[IO.Path]::GetFullPath($OutputDirectory)
$options=''
if($LocalGlyphHeader){Copy-Item -LiteralPath $LocalGlyphHeader -Destination (Join-Path $output 'LocalGlyphs.h') -Force;$options='/DCOMPANION_LOCAL_GLYPHS /I"'+$output+'"'}
$script=Join-Path $output 'compile-widget.cmd'
$source=Join-Path $root 'src/NativeWidget/Standalone.cpp'
$commands=@('@echo off','chcp 65001 >nul',('call "'+$vars+'" >nul'),('cl /nologo /std:c++20 /utf-8 /EHsc /O2 /MT /DUNICODE /D_UNICODE /D_WIN32_WINNT=0x0A00 '+$options+' "'+$source+'" /Fo:"'+(Join-Path $output 'Widget.obj')+'" /Fe:"'+(Join-Path $output 'AirPodsCompanion.exe')+'" /link /SUBSYSTEM:WINDOWS user32.lib gdi32.lib shell32.lib advapi32.lib d2d1.lib dwrite.lib dwmapi.lib ole32.lib windowscodecs.lib'),'exit /b %errorlevel%')
[IO.File]::WriteAllText($script,($commands -join "`r`n"),[Text.UTF8Encoding]::new($false))
& $env:ComSpec /d /c $script
if($LASTEXITCODE -ne 0){throw 'The native widget build failed.'}
Copy-Item -LiteralPath (Join-Path $root 'CREDITS.md'),(Join-Path $root 'LICENSE') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $output -Recurse -Force
