$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot 'tests\CodexUsageCard.Tests.csproj'
dotnet build $project -c Release
if($LASTEXITCODE -ne 0){throw 'Test build failed'}
$exe=Join-Path $PSScriptRoot 'tests\bin\Release\net10.0-windows\CodexUsageCard.Tests.exe'
foreach($name in @('grid-probe','interaction-probe','assets-probe','features-probe','settings-probe','rows-probe','pending-paths','adaptation','session-routing')){
    & $exe ('--'+$name) (Join-Path $PSScriptRoot ('test-output\'+$name))
    if($LASTEXITCODE -ne 0){throw ('Failed: '+$name)}
}
