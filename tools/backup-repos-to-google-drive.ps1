param(
    [string]$RepoRoot = "..",
    [string]$ConfigPath = "appsettings.json",
    [string]$BackupFolder = "Repo Backups",
    [string[]]$RepoName,
    [switch]$KeepLocalArchives
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptRoot "RepoBackupToGoogleDrive\RepoBackupToGoogleDrive.csproj"
$resolvedRepoRoot = if ([System.IO.Path]::IsPathRooted($RepoRoot)) { $RepoRoot } else { Join-Path (Get-Location) $RepoRoot }
$resolvedConfigPath = if ([System.IO.Path]::IsPathRooted($ConfigPath)) { $ConfigPath } else { Join-Path (Get-Location) $ConfigPath }

$argsList = @(
    "run",
    "--project", $projectPath,
    "--",
    "--repo-root", $resolvedRepoRoot,
    "--config", $resolvedConfigPath,
    "--backup-folder", $BackupFolder
)

if ($KeepLocalArchives) {
    $argsList += "--keep-local"
}

foreach ($name in $RepoName) {
    $argsList += "--repo"
    $argsList += $name
}

& dotnet @argsList
exit $LASTEXITCODE
