param (
    [Parameter(Mandatory)]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$exe = Join-Path $package 'DatabaseManager.exe'
$log = Join-Path $package 'smoke.log'
foreach ($file in @($exe, (Join-Path $package 'sqlite3mc.dll'), (Join-Path $package 'Config/Template/Data/profiles.db3'))) {
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing Native AOT package file: $file" }
}
foreach ($name in @('coreclr.dll', 'hostfxr.dll', 'Profiles')) {
    if (Test-Path -LiteralPath (Join-Path $package $name)) { throw "Unexpected Native AOT package entry: $name" }
}

$work = (New-Item -ItemType Directory -Path (Join-Path $env:TEMP "dbm-aot-$([Guid]::NewGuid())")).FullName
Write-Host "Native AOT smoke results: $work"
foreach ($suite in @('p0', 'p2')) {
    $process = Start-Process -FilePath $exe -ArgumentList '--smoke', "--$suite" -WorkingDirectory $work -PassThru
    try {
        if (!$process.WaitForExit(180000)) {
            $process.Kill()
            throw "Native AOT $suite timed out"
        }
        if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $work "artifacts/$suite-ui/passed.txt"))) {
            throw "Native AOT $suite failed with exit code $($process.ExitCode)"
        }
        Write-Host "Native AOT $suite passed"
    }
    catch {
        if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log }
        throw
    }
    finally {
        $process.Dispose()
    }
}
