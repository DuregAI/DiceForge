param(
    [string]$DotnetPath = 'C:/Backforge/Tools/Dotnet8/dotnet.exe',
    [string]$WasiRoot = 'C:/Backforge/Tools/Wasi24',
    [string]$ToolHome = 'C:/Backforge/Tools'
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $ToolHome 'DotnetHome'
$env:NUGET_PACKAGES = Join-Path $ToolHome 'NugetPackages'
$moduleProject = Join-Path $PSScriptRoot 'spacetimedb/StdbModule.csproj'
$artifactRoot = Join-Path $ToolHome 'AdminBackendBuild'
& $DotnetPath build $moduleProject -c Release "-p:LocalWasiRoot=$WasiRoot" `
    "-p:BaseIntermediateOutputPath=$artifactRoot/obj/" "-p:BaseOutputPath=$artifactRoot/bin/"
if ($LASTEXITCODE -ne 0) { throw 'SpacetimeDB module build failed.' }
