param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectId,

    [Parameter(Mandatory = $true)]
    [string]$ServiceAccountEmail,

    [string]$OutputPath = "secrets/google-drive-service-account.json",

    [switch]$Overwrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-GCloudInstalled {
    $gcloud = Get-Command gcloud -ErrorAction SilentlyContinue
    if (-not $gcloud) {
        throw "gcloud CLI is not installed or not in PATH. Install Google Cloud SDK first."
    }
}

function Assert-GCloudAuthenticated {
    $account = (& gcloud auth list --filter=status:ACTIVE --format="value(account)") 2>$null
    if (-not $account) {
        throw "No active gcloud account found. Run: gcloud auth login"
    }
}

function Resolve-OutputPath([string]$PathValue) {
    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return $PathValue
    }

    return Join-Path -Path (Get-Location) -ChildPath $PathValue
}

Assert-GCloudInstalled
Assert-GCloudAuthenticated

$resolvedOutputPath = Resolve-OutputPath -PathValue $OutputPath
$outputDirectory = Split-Path -Path $resolvedOutputPath -Parent
if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

if ((Test-Path $resolvedOutputPath) -and (-not $Overwrite)) {
    throw "Output file already exists: $resolvedOutputPath`nUse -Overwrite to replace it."
}

Write-Host "Generating service-account key JSON..." -ForegroundColor Cyan
& gcloud iam service-accounts keys create $resolvedOutputPath `
    --iam-account=$ServiceAccountEmail `
    --project=$ProjectId `
    --key-file-type=json

if (-not (Test-Path $resolvedOutputPath)) {
    throw "Key file was not created."
}

$json = Get-Content -Path $resolvedOutputPath -Raw | ConvertFrom-Json
if (-not $json.client_email -or -not $json.private_key) {
    throw "Generated file does not contain expected fields (client_email/private_key)."
}

Write-Host "Key generated successfully: $resolvedOutputPath" -ForegroundColor Green
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1) Share your Google Drive parent folder with: $($json.client_email)" -ForegroundColor Yellow
Write-Host "2) Keep this file private. It is git-ignored by default." -ForegroundColor Yellow
