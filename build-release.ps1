$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$project = Join-Path $root "src\FortniteCinematicSettings\FortniteCinematicSettings.csproj"
$testProject = Join-Path $root "tests\FortniteCinematicSettings.Tests\FortniteCinematicSettings.Tests.csproj"
$publish = Join-Path $root "publish"
$release = Join-Path $root "release"
$iscc = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
if (!(Test-Path $iscc)) {
    $iscc = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
}

if (!$publish.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean a publish directory outside the project root."
}

Remove-Item -LiteralPath $publish -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publish, $release | Out-Null

dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw "Application build failed." }

dotnet run --project $testProject -c Release
if ($LASTEXITCODE -ne 0) { throw "Settings service smoke tests failed." }

dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish
if ($LASTEXITCODE -ne 0) { throw "Application publish failed." }

if (!(Test-Path $iscc)) {
    throw "Inno Setup compiler not found at $iscc"
}

& $iscc (Join-Path $root "installer\FortniteCinematicSettings.iss")
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }

Write-Host "Release executable: $publish\FortniteGraphicsMasterUtility.exe"
Write-Host "Installer: $release\FortniteGraphicsMasterUtility-Setup.exe"
