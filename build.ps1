param([switch]$Standalone)
$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot 'src\CodexRail.csproj'
$target=if($Standalone){'standalone'}else{'small'}
$flag=$Standalone.IsPresent.ToString().ToLowerInvariant()
dotnet publish $project -c Release -r win-x64 --self-contained $flag ('-p:EnableCompressionInSingleFile='+$flag) -o (Join-Path $PSScriptRoot ('publish\'+$target))
if($LASTEXITCODE -ne 0){throw 'Build failed'}
