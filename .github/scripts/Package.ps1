[CmdletBinding()]
param([Parameter(Mandatory)][string]$SourceCheckout,[Parameter(Mandatory)][string]$OutputDirectory,[Parameter(Mandatory)][string]$DalamudDirectory)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $SourceCheckout).ProviderPath
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ($output -eq $root -or $output.StartsWith($root + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Package output must be outside the source checkout.' }
$release = Join-Path $root 'NativeChatTabs/bin/Release'
$api = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $DalamudDirectory 'Dalamud.dll')).Version.Major
if ($api -ne 15) { throw 'Unsupported Dalamud API.' }
$manifest = Get-Content -LiteralPath (Join-Path $release 'NativeChatTabs.json') -Raw | ConvertFrom-Json -AsHashtable
$version = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $release 'NativeChatTabs.dll')).Version.ToString()
if ([version]$version -le [version]'0.0.0.0') { throw 'Invalid plugin version.' }
$raw = 'https://raw.githubusercontent.com/WayneXXDD/NativeChatTabs/plugin-repo'
$manifest.Name = 'Native Chat Tabs (Wayne, Testing)'
$manifest.AssemblyVersion = $version
$manifest.TestingAssemblyVersion = $version
$manifest.DalamudApiLevel = $api
$manifest.TestingDalamudApiLevel = $api
$manifest.RepoUrl = 'https://github.com/WayneXXDD/NativeChatTabs'
$manifest.IsTestingExclusive = $true
$manifest.IsHide = $false
$manifest.LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
foreach ($key in @('DownloadLinkInstall','DownloadLinkUpdate','DownloadLinkTesting')) { $manifest[$key] = "$raw/plugins/NativeChatTabs/latest.zip" }
$pluginDirectory = Join-Path $output 'plugins/NativeChatTabs'
$stage = Join-Path ([IO.Path]::GetDirectoryName($output)) ('nct-package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $pluginDirectory,$stage,(Join-Path $stage 'licenses') -Force | Out-Null
try {
    foreach ($name in @('NativeChatTabs.dll','KamiToolKit.dll','NativeChatTabs.deps.json')) {
        Copy-Item -LiteralPath (Join-Path $release $name) -Destination $stage
    }
    Copy-Item -LiteralPath (Join-Path $release 'Assets') -Destination $stage -Recurse
    Copy-Item -LiteralPath (Join-Path $root 'NativeChatTabs/README.zh-TW.md'),(Join-Path $root 'NativeChatTabs/THIRD-PARTY-NOTICES.md') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $root 'NativeChatTabs/LICENSE') -Destination (Join-Path $stage 'licenses/NativeChatTabs-AGPL-3.0.txt')
    Copy-Item -LiteralPath (Join-Path $root 'dependencies/KamiToolKit/LICENSE') -Destination (Join-Path $stage 'licenses/KamiToolKit-MIT.txt')
    $utf8 = [Text.UTF8Encoding]::new($false)
    $json = (ConvertTo-Json -InputObject $manifest -Depth 20) + "`n"
    [IO.File]::WriteAllText((Join-Path $stage 'NativeChatTabs.json'),$json,$utf8)
    [IO.File]::WriteAllText((Join-Path $pluginDirectory 'NativeChatTabs.json'),$json,$utf8)
    $zip = Join-Path $pluginDirectory 'latest.zip'
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $names = @($archive.Entries.FullName | ForEach-Object { $_.Replace('\','/') })
        foreach ($name in @('NativeChatTabs.dll','NativeChatTabs.json','KamiToolKit.dll','licenses/KamiToolKit-MIT.txt','licenses/NativeChatTabs-AGPL-3.0.txt')) {
            if ($names -notcontains $name) { throw "Missing package entry: $name" }
        }
        $reader = [IO.StreamReader]::new($archive.GetEntry('NativeChatTabs.json').Open())
        try { $inside = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if ($inside.AssemblyVersion -ne $version -or $inside.InternalName -ne 'NativeChatTabs') { throw 'Package manifest mismatch.' }
    } finally { $archive.Dispose() }
    [IO.File]::WriteAllText((Join-Path $output 'build-manifest.json'),(ConvertTo-Json -InputObject @($manifest) -Depth 20) + "`n",$utf8)
    [IO.File]::WriteAllText((Join-Path $output 'README.md'),"Native Chat Tabs testing distribution. Install through https://raw.githubusercontent.com/WayneXXDD/DalamudPlugins/main/pluginmaster.json . Source: https://github.com/WayneXXDD/NativeChatTabs . Enable Get plugin testing builds in Dalamud settings.",$utf8)
} finally {
    $resolved = (Resolve-Path -LiteralPath $stage).ProviderPath
    $expected = [IO.Path]::GetFullPath($stage)
    if ($resolved -ne $expected -or (Get-Item -LiteralPath $resolved -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unexpected staging path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
