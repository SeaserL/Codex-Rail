$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw 'Native watcher requires Visual C++ Build Tools'}
$build=Join-Path $root 'publish/watcher'
New-Item -ItemType Directory -Force $build | Out-Null
$cmd='call "'+(Join-Path $vs 'VC/Auxiliary/Build/vcvars64.bat')+'" >nul && cl /nologo /O1 /MT /EHsc "'+(Join-Path $root 'native/Watcher.cpp')+'" /Fo"'+(Join-Path $build 'Watcher.obj')+'" /Fe"'+(Join-Path $build 'CodexRail.Watcher.exe')+'" /link /SUBSYSTEM:WINDOWS user32.lib shell32.lib'
& cmd.exe /c $cmd
if($LASTEXITCODE -ne 0){throw 'Watcher build failed'}