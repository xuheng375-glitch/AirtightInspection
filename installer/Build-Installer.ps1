$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$publishDir = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot 'publish\win-x64'))
$installerDir = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot 'installer'))
$projectFile = Join-Path $projectRoot 'AirtightInspection.WinForms\AirtightInspection.WinForms.csproj'
$innoScript = Join-Path $PSScriptRoot 'AirtightInspection.iss'

foreach ($target in @($publishDir, $installerDir)) {
    if (-not $target.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a directory outside the artifacts root: $target"
    }
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    New-Item -ItemType Directory -Path $target | Out-Null
}

dotnet publish $projectFile -c Release -r win-x64 --self-contained true -o $publishDir `
    -p:DebugSymbols=false -p:DebugType=None
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 command-line compiler ISCC.exe was not found.' }

$languageFile = Join-Path (Split-Path $iscc -Parent) 'Languages\ChineseSimplified.isl'
if (-not (Test-Path -LiteralPath $languageFile)) {
    $languageUrl = 'https://raw.githubusercontent.com/kira-96/Inno-Setup-Chinese-Simplified-Translation/1ff90acc4ed4aee82b1cda43253243deee3daed4/ChineseSimplified.isl'
    Invoke-WebRequest -UseBasicParsing -Uri $languageUrl -OutFile $languageFile
}

$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$compilerOutputDir = [System.IO.Path]::GetFullPath((Join-Path $tempRoot ('AirtightInspectionInstaller_' + [Guid]::NewGuid().ToString('N'))))
if (-not $compilerOutputDir.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a compiler output directory outside the system temp root: $compilerOutputDir"
}
New-Item -ItemType Directory -Path $compilerOutputDir | Out-Null
try {
    # Compiling directly into a watched Desktop folder can cause antivirus to
    # lock Setup.exe while Inno Setup updates its resources (Win32 error 110).
    & $iscc "/O$compilerOutputDir" $innoScript
    if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed with exit code $LASTEXITCODE" }

    $compiledSetup = Get-ChildItem -LiteralPath $compilerOutputDir -Filter '*.exe' | Select-Object -First 1
    if (-not $compiledSetup) { throw 'Installer compilation completed without an output EXE.' }
    Copy-Item -LiteralPath $compiledSetup.FullName -Destination (Join-Path $installerDir $compiledSetup.Name) -Force
}
finally {
    if (Test-Path -LiteralPath $compilerOutputDir) {
        Remove-Item -LiteralPath $compilerOutputDir -Recurse -Force
    }
}

$setup = Get-ChildItem -LiteralPath $installerDir -Filter '*.exe' | Select-Object -First 1
if (-not $setup) { throw 'Installer copy completed without an output EXE.' }
Write-Output "Installer created: $($setup.FullName)"
