[CmdletBinding()]
param(
    [ValidateSet('Infrastructure','Api','Ui','Live')][string]$Suite='Infrastructure',
    [ValidateSet('All','1','2','3')][string]$Level='All',
    [string]$Dotnet='dotnet',
    [string]$Python='python',
    [string]$EnvFile,
    [string]$ResultsDirectory
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if(-not $ResultsDirectory){$ResultsDirectory=Join-Path $projectRoot ('test-results/'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))}
$ResultsDirectory=[IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
$env:AQA_RESULTS_DIR=$ResultsDirectory
if($EnvFile){$env:AQA_ENV_FILE=[IO.Path]::GetFullPath($EnvFile)}
$filter=if($Suite -eq 'Live'){'(TestCategory=Api|TestCategory=Ui)'}else{'TestCategory='+$Suite}
if($Level -ne 'All' -and $Suite -ne 'Infrastructure'){$filter+='&TestCategory=Level'+$Level}
$testExit=1
Push-Location $projectRoot
try {
    & $Dotnet restore --locked-mode
    if($LASTEXITCODE -eq 0){
        & $Dotnet test --no-restore --settings .runsettings --filter $filter --logger 'trx;LogFileName=results.trx' --results-directory $ResultsDirectory
        $testExit=$LASTEXITCODE
    }
}
finally {
    & $Python (Join-Path $PSScriptRoot 'report.py') $ResultsDirectory
    $reportExit=$LASTEXITCODE
    Pop-Location
}
Write-Host ('Report: '+(Join-Path $ResultsDirectory 'index.html'))
if($testExit -ne 0 -or $reportExit -ne 0){exit 1}
exit 0
