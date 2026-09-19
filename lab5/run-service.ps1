param(
    [string]$Dotnet = 'dotnet',
    [ValidateSet('run','setup','test')][string]$Action = 'run',
    [ValidateSet('mod','consistent')][string]$Strategy = 'mod',
    [int]$UserId = 1
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
# Settings affect only this process and child processes, never the saved project configuration.
$values = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ASPNETCORE_URLS = 'http://localhost:5085'
    Sharding__Enabled = 'true'
    Sharding__Strategy = $Strategy
    Sharding__VirtualNodes = '100'
    ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5437;Database=test_project_db;Username=test_project_db;Password=test_project_db'
    ConnectionStrings__ReplicaConnection = 'Host=localhost;Port=5438;Database=test_project_db;Username=test_project_db;Password=test_project_db'
    ConnectionStrings__Redis = 'localhost:6379'
    LAB_USER_ID = "$UserId"
    LAB_SHARD_STRATEGY = $Strategy
}
foreach ($i in 0..2) {
    $database = if ($Strategy -eq 'consistent') { 'tarot_ring' } else { 'tarot_shard' }
    $connection = "Host=localhost;Port=$(5441+$i);Database=$database;Username=tarot;Password=tarot"
    $values["Sharding__Connections__$i"] = $connection
    $values["LAB_SHARD_$i"] = $connection
}
$previous = @{}
try {
    foreach ($key in $values.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $values[$key], 'Process')
    }
    if ($Action -eq 'test') {
        & $Dotnet test (Join-Path $root 'test_project.csproj')
    } elseif ($Action -eq 'setup') {
        & $Dotnet run --project (Join-Path $root 'test_project.csproj') --no-launch-profile -- --shard-lab-setup
    } else {
        & $Dotnet run --project (Join-Path $root 'test_project.csproj') --no-launch-profile
    }
    if ($LASTEXITCODE -ne 0) { throw "dotnet exited with code $LASTEXITCODE" }
} finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}
