$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
$secretDirectory = Join-Path $env:LOCALAPPDATA 'MinimalApiEntity\Postgres'
$passwordFile = Join-Path $secretDirectory 'password.txt'

Get-Command docker -ErrorAction Stop | Out-Null
Get-Command dotnet -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Path $secretDirectory -Force | Out-Null

$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$acl = [System.Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true, $false)
$rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
    $identity.User, 'FullControl', 'ContainerInherit, ObjectInherit', 'None', 'Allow')
$acl.SetAccessRule($rule)
[System.IO.FileSystemAclExtensions]::SetAccessControl(
    [System.IO.DirectoryInfo]::new($secretDirectory), $acl)

if (-not (Test-Path -LiteralPath $passwordFile)) {
    $password = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    [System.IO.File]::WriteAllText($passwordFile, $password)
}
$password = [System.IO.File]::ReadAllText($passwordFile).Trim()
if ([string]::IsNullOrWhiteSpace($password)) {
    throw 'The PostgreSQL password file is empty.'
}

$env:PIZZA_POSTGRES_PASSWORD_FILE = $passwordFile
docker compose -p minimalapientity-pizza -f (Join-Path $projectDirectory 'compose.yaml') up -d --wait
if ($LASTEXITCODE -ne 0) {
    throw 'PostgreSQL startup failed.'
}

docker compose -p minimalapientity-pizza -f (Join-Path $projectDirectory 'compose.yaml') exec -T postgres sh -c 'PGPASSWORD="$(cat /run/secrets/postgres_password)" psql -h 127.0.0.1 -U pizza_app -d pizzas -v ON_ERROR_STOP=1 -c "SELECT 1"' | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'PostgreSQL authentication failed. The saved password must match the existing database volume.'
}

$settings = @{
    'ConnectionStrings:Pizzas' = "Host=127.0.0.1;Port=55433;Database=pizzas;Username=pizza_app;Password=$password"
}
$settings | ConvertTo-Json -Compress | dotnet user-secrets set --project (Join-Path $projectDirectory 'MinimalApiEntity.csproj')
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to configure the PostgreSQL connection string in user secrets.'
}
Write-Output 'PostgreSQL is healthy on localhost:55433. Development credentials are stored outside the project.'
