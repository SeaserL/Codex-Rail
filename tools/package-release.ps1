param([string]$Version='0.14.1')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& (Join-Path $root 'build.ps1')
& (Join-Path $root 'build.ps1') -Standalone
$dist=Join-Path $root 'dist'
$bundle=Join-Path $dist ('CodexRail-v'+$Version+'-win-x64')
New-Item -ItemType Directory -Force $bundle | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'publish/small/CodexRail.exe') -Destination (Join-Path $bundle 'CodexRail-win-x64.exe')
Copy-Item -LiteralPath (Join-Path $root 'publish/standalone/CodexRail.exe') -Destination (Join-Path $bundle 'CodexRail-win-x64-standalone.exe')
foreach($name in @('LICENSE','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $bundle}
Copy-Item -LiteralPath (Join-Path $root 'docs/INSTALL.md') -Destination $bundle
$assets=Get-Content -LiteralPath (Join-Path $root 'src/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
foreach($package in @('microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64')){
    $dependencies=@($assets.project.frameworks.Values | ForEach-Object { $_.downloadDependencies } | Where-Object { $_.name.ToLowerInvariant() -eq $package })
    $versionText=$dependencies[0].version.Trim('[',']').Split(',')[0].Trim()
    if(!$versionText){throw ('Missing restored runtime version: '+$package)}
    $candidates=@()
    foreach($folder in $assets.packageFolders.Keys){
        $parent=Join-Path $folder $package
        if(Test-Path -LiteralPath $parent){$candidates+=Get-ChildItem -LiteralPath $parent -Directory | Where-Object {$_.Name -eq $versionText} }
    }
    $pack=$candidates | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if(!$pack){throw ('Missing runtime license package: '+$package)}
    $destination=Join-Path $bundle ('licenses/'+$package+'/'+$pack.Name)
    New-Item -ItemType Directory -Force $destination | Out-Null
    $license=Get-ChildItem -LiteralPath $pack.FullName -File | Where-Object {$_.Name -match '^LICENSE(\.TXT)?$'}
    if(!$license){throw ('Missing license: '+$package)}
    $license | Copy-Item -Destination $destination
    Get-ChildItem -LiteralPath $pack.FullName -File | Where-Object {$_.Name -match '^THIRD-PARTY-NOTICES'} | Copy-Item -Destination $destination
}
$hashes=Get-ChildItem -LiteralPath $bundle -Filter *.exe | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$_.Name }
$hashes | Set-Content -LiteralPath (Join-Path $bundle 'SHA256SUMS.txt') -Encoding utf8
Compress-Archive -Path (Join-Path $bundle '*') -DestinationPath ($bundle+'.zip') -Force
Write-Output $bundle
