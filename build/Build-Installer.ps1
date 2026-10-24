param([string]$Configuration = 'Release', [string]$IsccPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
if (!$IsccPath) {
 $candidates = @((Join-Path $root '.tools/InnoSetup/ISCC.exe'), "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe", "$env:ProgramFiles/Inno Setup 6/ISCC.exe", "$env:ProgramFiles/Inno Setup 7/ISCC.exe")
 $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$IsccPath) { throw 'Install Inno Setup 6.5+ from https://jrsoftware.org/isdl.php or pass -IsccPath.' }
$publish = Join-Path $root 'dist/publish'
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:MSBuildEnableWorkloadResolver = 'false'
try {
 dotnet publish (Join-Path $root 'native/Datto.csproj') -c $Configuration --self-contained false -o $publish --disable-build-servers -p:UseSharedCompilation=false
 if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
 & $IsccPath "/DPublishDir=$publish" (Join-Path $PSScriptRoot 'Datto.iss')
 if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
} finally { dotnet build-server shutdown }
