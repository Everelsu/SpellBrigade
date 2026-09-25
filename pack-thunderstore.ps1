# Builds one Thunderstore zip per mod: <Mod>/thunderstore/<name>-<version>.zip
# Needs a Release build first (dotnet build -c Release). Run: powershell -File pack-thunderstore.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

foreach ($dir in Get-ChildItem $PSScriptRoot -Directory | Where-Object { Test-Path "$($_.FullName)\thunderstore\manifest.json" }) {
    $ts = "$($dir.FullName)\thunderstore"
    $manifest = Get-Content "$ts\manifest.json" -Raw | ConvertFrom-Json
    $csproj = [xml](Get-Content (Get-ChildItem "$($dir.FullName)\*.csproj").FullName)
    $csVersion = @($csproj.Project.PropertyGroup.Version | Where-Object { $_ })[0]
    if ($manifest.version_number -ne $csVersion) { throw "$($dir.Name): manifest $($manifest.version_number) != csproj $csVersion" }

    $dll = "$($dir.FullName)\bin\Release\net6.0\$($manifest.name).dll"
    if (-not (Test-Path $dll)) { throw "$($dir.Name): $dll not found, build Release first" }

    $zipPath = "$ts\$($manifest.name)-$($manifest.version_number).zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath }
    $zip = [IO.Compression.ZipFile]::Open($zipPath, 'Create')
    try {
        # entry names are set by hand: Thunderstore wants forward slashes, Compress-Archive on PS 5.1 writes backslashes
        foreach ($f in 'manifest.json', 'README.md', 'icon.png') {
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, "$ts\$f", $f)
        }
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dll, "Mods/$($manifest.name).dll")
    } finally { $zip.Dispose() }
    "$zipPath"
}
