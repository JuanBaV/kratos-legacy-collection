param([switch]$Publish)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    dotnet build KratosLegacyCollection.sln -c Release -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet run --project tests/KratosLegacyCollection.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
    dotnet run --project tests/KratosLegacyCollection.UiChecks -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'WPF checks failed.' }
    if ($Publish) {
        dotnet publish src/KratosLegacyCollection.App -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o artifacts/portable-win-x64
        if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed.' }
        Copy-Item -LiteralPath LICENSE,README.md,CREDITS.md -Destination artifacts/portable-win-x64
        $assets = Get-Content src/KratosLegacyCollection.App/obj/project.assets.json -Raw | ConvertFrom-Json
        $runtimeConfig = Get-Content artifacts/portable-win-x64/KratosLegacyCollection.runtimeconfig.json -Raw | ConvertFrom-Json
        $runtimePackages = @($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object { $_.name -in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App') } | ForEach-Object { $_.name + '.Runtime.win-x64/' + $_.version })
        if ($runtimePackages.Count -ne 2) { throw 'Runtime package notices could not be identified.' }
        foreach ($package in $runtimePackages) {
            $relative = $package.ToLowerInvariant().Replace('/', [IO.Path]::DirectorySeparatorChar)
            $packageFolder = @($assets.packageFolders.PSObject.Properties.Name | ForEach-Object { Join-Path $_ $relative } | Where-Object { Test-Path -LiteralPath $_ })[0]
            if (-not $packageFolder) { throw "Runtime package folder missing: $package" }
            $notices = @(Get-ChildItem -LiteralPath $packageFolder -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)(\..*)?$' })
            if ($notices.Count -eq 0) { throw "Runtime license missing: $package" }
            $destination = Join-Path artifacts/portable-win-x64/licenses ($package -replace '/', '-')
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            $notices | Copy-Item -Destination $destination
        }
    }
} finally { Pop-Location }
