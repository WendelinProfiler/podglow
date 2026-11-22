param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$installation=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$installation){throw 'MSVC and the Windows SDK are required.'}
$output=Join-Path $root 'out/tray-wheel-tests'
New-Item -ItemType Directory -Path $output -Force|Out-Null
$vars=Join-Path $installation 'VC/Auxiliary/Build/vcvars64.bat'
$source=Join-Path $root 'tests/NativeTrayWheel.cpp'
$exe=Join-Path $output 'Test-TrayWheel.exe'
$commands=@('@echo off',('call "'+$vars+'" >nul'),('cl /nologo /std:c++20 /EHsc /MT /DUNICODE /D_UNICODE "'+$source+'" /Fo:"'+(Join-Path $output 'test.obj')+'" /Fe:"'+$exe+'" /link user32.lib shell32.lib'),'exit /b %errorlevel%')
$file=Join-Path $output 'build.cmd'
[IO.File]::WriteAllLines($file,$commands)
& $env:ComSpec /d /c $file
if($LASTEXITCODE){throw 'Tray wheel test compilation failed.'}
& $exe
if($LASTEXITCODE){throw 'Tray wheel routing tests failed.'}
