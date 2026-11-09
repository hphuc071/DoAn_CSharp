# One-shot isolated closure fixture. Never reuses/reset/reseeds a historical fixture.
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $root
if((git branch --show-current) -ne 'feat/thai-web-cart-order'){throw 'Wrong branch'}
$dir=Join-Path $root 'artifacts/uc-web-06/sql-consent-v4'
New-Item -ItemType Directory -Force $dir|Out-Null
$private=Join-Path $dir 'private/credentials.clixml'
if(Test-Path -LiteralPath $private){throw 'Existing closure credential evidence: no reuse'}
if(Test-Path -LiteralPath (Join-Path $dir 'write-once.json')){throw 'Previous write attempt: no retry'}
$name='fourchang-uc06-consent-fixture-v4'
$existing=@(docker ps -aq --filter "name=^/$name`$")
if($LASTEXITCODE -ne 0 -or ($existing|Where-Object {$_}).Count){throw 'Existing fixture or Docker guard failure'}
$ports=@(docker ps --format '{{.Ports}}')
if($LASTEXITCODE -ne 0 -or ($ports|Where-Object {$_ -match ':14339->'}).Count){throw 'Port unavailable'}
$raw=docker image inspect mcr.microsoft.com/mssql/server:2022-latest
if($LASTEXITCODE -ne 0){throw 'Local SQL2022 image required'}
$image=($raw|ConvertFrom-Json)[0].Id
$utf8=[Text.UTF8Encoding]::new($false)
$baseline=@(docker ps -a --format '{{.Names}}|{{.ID}}|{{.Status}}|{{.Ports}}')
[IO.File]::WriteAllLines((Join-Path $dir 'containers-before.txt'),$baseline,$utf8)
$owned=$null
$oldPos=$env:FOURCHANG_POS_KEY;$oldCustomer=$env:UC06_CUSTOMER_PASSWORD;
$oldSa=$env:UC06_FIXTURE_SA_PASSWORD;$oldApp=$env:UC06_FIXTURE_APP_PASSWORD;$oldDocker=$env:MSSQL_SA_PASSWORD
try {
 $rng=[Security.Cryptography.RandomNumberGenerator]::Create();$bytes=New-Object byte[] 32
 $rng.GetBytes($bytes);$env:UC06_FIXTURE_SA_PASSWORD='Cl!'+[Convert]::ToBase64String($bytes)+'9a'
 $rng.GetBytes($bytes);$env:UC06_FIXTURE_APP_PASSWORD='Cl!'+[Convert]::ToBase64String($bytes)+'8b';$rng.GetBytes($bytes);$env:FOURCHANG_POS_KEY=[Convert]::ToBase64String($bytes);$rng.GetBytes($bytes);$env:UC06_CUSTOMER_PASSWORD='Ts!'+[Convert]::ToBase64String($bytes);$rng.Dispose()
 New-Item -ItemType Directory -Force (Split-Path -Parent $private)|Out-Null
 git check-ignore --quiet artifacts/uc-web-06/sql-consent-v4/private/credentials.clixml
 if($LASTEXITCODE -ne 0){throw 'Credential path must be ignored'}
 @([PSCredential]::new('sa',(ConvertTo-SecureString $env:UC06_FIXTURE_SA_PASSWORD -AsPlainText -Force)),[PSCredential]::new('uc06_consent',(ConvertTo-SecureString $env:UC06_FIXTURE_APP_PASSWORD -AsPlainText -Force)))|Export-Clixml -LiteralPath $private
 $env:MSSQL_SA_PASSWORD=$env:UC06_FIXTURE_SA_PASSWORD
 $runId=[Guid]::NewGuid().ToString()
 $owned=docker run --detach --name $name --hostname fc-uc06-consent-fixture-v4 --label com.docker.compose.project=fourchang-uc06-consent-fixture-v4 --label "fourchang.fixture.runid=$runId" --publish 127.0.0.1:14339:1433 --env ACCEPT_EULA=Y --env MSSQL_PID=Developer --env MSSQL_SA_PASSWORD $image
 if($LASTEXITCODE -ne 0){$owned=$null;throw 'New fixture failed: no SQL sent'}
 $inspect=docker inspect $owned;if($LASTEXITCODE -ne 0){throw 'Owned fixture inspect failed'}
 $container=($inspect|ConvertFrom-Json)[0]
 $proof=[ordered]@{Name=$container.Name.TrimStart('/');Id=$container.Id;Hostname=$container.Config.Hostname;Project=$container.Config.Labels.'com.docker.compose.project';RunId=$runId;ImageId=$container.Image;Ports=@($container.NetworkSettings.Ports.'1433/tcp');CapturedUtc=[DateTime]::UtcNow.ToString('O')}
 if($proof.Name -ne $name -or $proof.Hostname -ne 'fc-uc06-consent-fixture-v4' -or $proof.Project -ne $name -or $container.Config.Labels.'fourchang.fixture.runid' -ne $runId -or $proof.Ports.Count -ne 1 -or $proof.Ports[0].HostIp -ne '127.0.0.1' -or $proof.Ports[0].HostPort -ne '14339'){throw 'Docker target guard FAIL'}
 [IO.File]::WriteAllText((Join-Path $dir 'container.json'),($proof|ConvertTo-Json -Depth 6),$utf8)
 $ready=$false
 for($i=0;$i -lt 45;$i++){$logs=docker logs $owned 2>&1;if(($logs -join "`n").Contains('SQL Server is now ready for client connections')){$ready=$true;break};Start-Sleep -Seconds 1}
 if(-not $ready){throw 'SQL readiness timeout; no provisioning'}
 $dll='tests/FourChang.CheckoutSql/bin/Debug/net8.0/FourChang.CheckoutSql.dll'
 $sources=Join-Path $dir 'runner-source'
 New-Item -ItemType Directory -Force $sources|Out-Null
 Get-ChildItem -LiteralPath $PSScriptRoot -File|Where-Object {$_.Extension -in '.cs','.csproj','.ps1'}|Copy-Item -Destination $sources
 Copy-Item -LiteralPath $dll -Destination (Join-Path $dir 'runner.dll')
 [IO.File]::WriteAllText((Join-Path $dir 'runner-sha256.txt'),(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash,$utf8)
 dotnet $dll --run-once|Tee-Object -FilePath (Join-Path $dir 'run.log')
 if($LASTEXITCODE -ne 0){throw 'Closure STOPPED_NO_RETRY; preserve result.json and fixture'}
} finally {
 if($owned){docker stop $owned|Out-Null;if($LASTEXITCODE -ne 0){Write-Warning 'Could not stop the owned fixture'}
  $after=docker inspect $owned
  if($LASTEXITCODE -eq 0){$c=($after|ConvertFrom-Json)[0];[IO.File]::WriteAllText((Join-Path $dir 'container-final.json'),(@{Id=$c.Id;Name=$c.Name;Running=$c.State.Running;FinishedAt=$c.State.FinishedAt}|ConvertTo-Json),$utf8)}
 }
 [IO.File]::WriteAllLines((Join-Path $dir 'containers-after.txt'),@(docker ps -a --format '{{.Names}}|{{.ID}}|{{.Status}}|{{.Ports}}'),$utf8)
 $env:FOURCHANG_POS_KEY=$oldPos;$env:UC06_CUSTOMER_PASSWORD=$oldCustomer;$env:UC06_FIXTURE_SA_PASSWORD=$oldSa;$env:UC06_FIXTURE_APP_PASSWORD=$oldApp;$env:MSSQL_SA_PASSWORD=$oldDocker
}
