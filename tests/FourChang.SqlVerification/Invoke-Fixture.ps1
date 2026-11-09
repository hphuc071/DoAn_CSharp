param([Parameter(Mandatory=$true)][ValidateSet('Create','Identity','Provision','Run','Stop')][string]$Mode)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $root
if ((git branch --show-current) -ne 'feat/thai-web-cart-order') { throw 'Wrong branch' }
$proofPath=Join-Path $root 'artifacts/uc-web-07-08/sql-fixture-v2-container.json'
$privatePath=Join-Path $root 'artifacts/uc-web-07-08/private/uc0708-fixture-v2.clixml'
$utf8=New-Object Text.UTF8Encoding($false)
function Save-Proof($container,$runId) {
    $proof=[ordered]@{Name=$container.Name.TrimStart('/');Id=$container.Id;Hostname=$container.Config.Hostname;
        Project=$container.Config.Labels.'com.docker.compose.project';RunId=$runId;
        ImageId=$container.Image;Ports=@($container.NetworkSettings.Ports.'1433/tcp');CapturedUtc=[DateTime]::UtcNow.ToString('O')}
    if($proof.Name -ne 'fourchang-uc0708-fixture-v2' -or $proof.Hostname -ne 'fc-uc0708-fixture-v2' -or
        $proof.Project -ne 'fourchang-uc0708-fixture-v2' -or $container.Config.Labels.'fourchang.fixture.runid' -ne $runId -or
        $proof.Ports.Count -ne 1 -or $proof.Ports[0].HostIp -ne '127.0.0.1' -or $proof.Ports[0].HostPort -ne '14333') {throw 'Fixture Docker guard failed'}
    [IO.File]::WriteAllText($proofPath,($proof|ConvertTo-Json -Depth 5),$utf8)
}
function Inspect-Owned {
    $proof=Get-Content -Raw -Encoding UTF8 -LiteralPath $proofPath|ConvertFrom-Json
    $raw=docker inspect $proof.Id
    if($LASTEXITCODE -ne 0){throw 'Fixture inspect failed'}
    $container=($raw|ConvertFrom-Json)[0]
    if($container.Id -ne $proof.Id){throw 'Fixture container identity changed'}
    Save-Proof $container $proof.RunId
    return $container
}
$oldSa=$env:UC0708_FIXTURE_SA_PASSWORD
$oldApp=$env:UC0708_FIXTURE_APP_PASSWORD
$oldDocker=$env:MSSQL_SA_PASSWORD
$oldManifest=$env:UC0708_APPROVED_MANIFEST_SHA256
try {
    if($Mode -eq 'Create') {
        $existing=@(docker ps -aq --filter 'name=^/fourchang-uc0708-fixture-v2$')
        if($LASTEXITCODE -ne 0 -or ($existing|Where-Object {$_}).Count -gt 0){throw 'Fixture name already exists; never reuse/reset it'}
        $ports=@(docker ps --format '{{.Ports}}')
        if($LASTEXITCODE -ne 0 -or ($ports|Where-Object {$_ -match ':14333->'}).Count -gt 0){throw 'Fixture port already allocated'}
        if(Test-Path -LiteralPath $privatePath){throw 'Existing fixture credentials; refusing overwrite'}
        $imageRaw=docker image inspect mcr.microsoft.com/mssql/server:2022-latest
        if($LASTEXITCODE -ne 0){throw 'Compatible local SQL2022 image required; no implicit pull'}
        $image=($imageRaw|ConvertFrom-Json)[0].Id
        $runId=[Guid]::NewGuid().ToString()
        $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
        $bytes=New-Object byte[] 32; $rng.GetBytes($bytes)
        $env:UC0708_FIXTURE_SA_PASSWORD='Fx!'+[Convert]::ToBase64String($bytes)+'9a'
        $rng.GetBytes($bytes); $env:UC0708_FIXTURE_APP_PASSWORD='Fx!'+[Convert]::ToBase64String($bytes)+'8b'; $rng.Dispose()
        $dir=Split-Path -Parent $privatePath
        New-Item -ItemType Directory -Force -Path $dir|Out-Null
        git check-ignore --quiet artifacts/uc-web-07-08/private/uc0708-fixture-v2.clixml
        if($LASTEXITCODE -ne 0){throw 'Credential path must be ignored'}
        @([PSCredential]::new('sa',(ConvertTo-SecureString $env:UC0708_FIXTURE_SA_PASSWORD -AsPlainText -Force)),
          [PSCredential]::new('uc0708_fixture',(ConvertTo-SecureString $env:UC0708_FIXTURE_APP_PASSWORD -AsPlainText -Force))) | Export-Clixml -LiteralPath $privatePath
        $env:MSSQL_SA_PASSWORD=$env:UC0708_FIXTURE_SA_PASSWORD
        $id=docker run --detach --name fourchang-uc0708-fixture-v2 --hostname fc-uc0708-fixture-v2 --label com.docker.compose.project=fourchang-uc0708-fixture-v2 --label "fourchang.fixture.runid=$runId" --publish 127.0.0.1:14333:1433 --env ACCEPT_EULA=Y --env MSSQL_PID=Developer --env MSSQL_SA_PASSWORD $image
        if($LASTEXITCODE -ne 0){throw 'New fixture container failed to start; no SQL commands sent'}
        $raw=docker inspect $id; if($LASTEXITCODE -ne 0){throw 'Created container inspect failed'}
        Save-Proof (($raw|ConvertFrom-Json)[0]) $runId
        $ready=$false
        for($i=0;$i -lt 40;$i++) {
            $log=docker logs $id 2>&1
            if(($log -join "`n").Contains('SQL Server is now ready for client connections')){$ready=$true;break}
            Start-Sleep -Seconds 1
        }
        if(-not $ready){throw 'Fixture SQL readiness timed out; no provisioning attempted'}
        Write-Output 'PASS: new fixture SQL2022 container ready on localhost14333; credentials encrypted locally, not logged.'
    } else {
        $container=Inspect-Owned
        if($Mode -eq 'Stop') {
            docker stop $container.Id|Out-Null
            if($LASTEXITCODE -ne 0){throw 'Owned fixture stop failed'}
            Write-Output 'Stopped only the owned fixture; data/container retained.'
        } else {
            if(-not $container.State.Running){throw 'Fixture is not running; no automatic restart'}
            $credentials=@(Import-Clixml -LiteralPath $privatePath)
            $env:UC0708_FIXTURE_SA_PASSWORD=($credentials|Where-Object {$_.UserName -eq 'sa'}).GetNetworkCredential().Password
            $env:UC0708_FIXTURE_APP_PASSWORD=($credentials|Where-Object {$_.UserName -eq 'uc0708_fixture'}).GetNetworkCredential().Password
            $dll='tests/FourChang.SqlVerification/bin/Debug/net8.0/FourChang.SqlVerification.dll'
            if($Mode -eq 'Identity') {
                dotnet $dll --identity-readonly|Tee-Object -FilePath artifacts/uc-web-07-08/sql-fixture-v2-identity.log
            } elseif($Mode -eq 'Provision') {
                dotnet $dll --provision|Tee-Object -FilePath artifacts/uc-web-07-08/sql-fixture-v2-provision.log
            } else {
                $manifest='artifacts/uc-web-07-08/sql-fixture-v2-manifest.json'
                $env:UC0708_APPROVED_MANIFEST_SHA256=(Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash
                dotnet $dll --run $manifest|Tee-Object -FilePath artifacts/uc-web-07-08/sql-integration-v2.log
            }
            if($LASTEXITCODE -ne 0){throw 'Fixture runner stopped; preserve evidence. No automatic retry/reset.'}
        }
    }
} finally {
    $env:UC0708_FIXTURE_SA_PASSWORD=$oldSa; $env:UC0708_FIXTURE_APP_PASSWORD=$oldApp
    $env:MSSQL_SA_PASSWORD=$oldDocker; $env:UC0708_APPROVED_MANIFEST_SHA256=$oldManifest
}
