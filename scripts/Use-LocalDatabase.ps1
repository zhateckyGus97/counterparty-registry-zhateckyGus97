# Dot-source this script to configure the current PowerShell session:
# . ./scripts/Use-LocalDatabase.ps1
$envPath = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'

if (-not (Test-Path -LiteralPath $envPath)) {
    $passwordBytes = New-Object byte[] 24
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($passwordBytes) }
    finally { $random.Dispose() }
    $password = [Convert]::ToBase64String($passwordBytes) + 'Aa1!'
    [System.IO.File]::WriteAllLines($envPath, @("MSSQL_SA_PASSWORD=$password", 'SQL_PORT=14333'))
    Write-Host 'Created .env with a random local SQL Server password.'
}

$settings = @{}
foreach ($line in Get-Content -LiteralPath $envPath) {
    if ($line -match '^\s*([^#=\s]+)=(.*)$') {
        $settings[$matches[1]] = $matches[2].Trim()
    }
}

if ([string]::IsNullOrWhiteSpace($settings['MSSQL_SA_PASSWORD'])) {
    throw 'Set MSSQL_SA_PASSWORD in .env.'
}

$sqlPort = 14333
if ($settings.ContainsKey('SQL_PORT')) {
    $sqlPort = [int]$settings['SQL_PORT']
}
if ($sqlPort -lt 1 -or $sqlPort -gt 65535) {
    throw 'SQL_PORT must be between 1 and 65535.'
}

$escapedPassword = $settings['MSSQL_SA_PASSWORD'].Replace('"', '""')
$env:SQL_PORT = [string]$sqlPort
$env:MSSQL_SA_PASSWORD = $settings['MSSQL_SA_PASSWORD']
$env:ConnectionStrings__DefaultConnection = 'Server=tcp:127.0.0.1,{0};Database=CounterpartyRegistry;User Id=sa;Password="{1}";Encrypt=True;TrustServerCertificate=True' -f $sqlPort, $escapedPassword

# Development profiles in Visual Studio read User Secrets independently of this terminal.
$apiProject = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/CounterpartyRegistry.Api/CounterpartyRegistry.Api.csproj'
$secrets = @{ ConnectionStrings = @{ DefaultConnection = $env:ConnectionStrings__DefaultConnection } }
$secrets | ConvertTo-Json -Depth 3 | dotnet user-secrets set --project $apiProject
if ($LASTEXITCODE -ne 0) {
    throw 'Could not save the local connection string to User Secrets.'
}

Write-Host "Local database configured on port $sqlPort for this session and Development launch profiles."
