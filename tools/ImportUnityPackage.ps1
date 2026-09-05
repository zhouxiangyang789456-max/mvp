param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'

$resolvedProject = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ProjectPath).Path)
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$assetsRoot = [System.IO.Path]::GetFullPath((Join-Path $resolvedProject 'Assets'))
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("unitypackage-" + [guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    & tar -xzf $resolvedPackage -C $tempRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to extract package: $resolvedPackage"
    }

    $imported = 0
    Get-ChildItem -LiteralPath $tempRoot -Directory | ForEach-Object {
        $entryDirectory = $_.FullName
        $pathnameFile = Join-Path $entryDirectory 'pathname'
        if (-not (Test-Path -LiteralPath $pathnameFile)) {
            return
        }

        # UnityPackage pathname records may end in either a NUL byte or the
        # literal trailer "\n00", depending on the package producer.
        $relativePath = (Get-Content -LiteralPath $pathnameFile -Raw) -replace "`r?`n00$", ''
        $relativePath = $relativePath.Trim([char]0, "`r", "`n")
        if (-not $relativePath.StartsWith('Assets/', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Package entry is outside Assets: $relativePath"
        }

        $destination = [System.IO.Path]::GetFullPath((Join-Path $resolvedProject ($relativePath -replace '/', '\')))
        if (-not $destination.StartsWith($assetsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and
            $destination -ne $assetsRoot) {
            throw "Unsafe package destination: $destination"
        }

        $assetFile = Join-Path $entryDirectory 'asset'
        $metaFile = Join-Path $entryDirectory 'asset.meta'

        if (Test-Path -LiteralPath $assetFile) {
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath $assetFile -Destination $destination -Force
        }
        else {
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
        }

        if (Test-Path -LiteralPath $metaFile) {
            Copy-Item -LiteralPath $metaFile -Destination ($destination + '.meta') -Force
        }

        $imported++
    }

    Write-Output "Imported $imported package entries from $resolvedPackage"
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
