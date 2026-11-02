#Requires -Version 7.0
param([Parameter(Mandatory)][string]$BaseUrl,[int]$WebPort=5184,[int]$ProxyPort=5185,
    [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot '../../DungChung/TestSupport.psm1') -Force
$root=Get-TestProjectRoot -StartPath $PSScriptRoot
$settings=Get-TestLocalSettings -ProjectRoot $root
$keys=@('FOURCHANG_API_URL','FOURCHANG_TEST_API_URL','FOURCHANG_TEST_PROXY_URL','FOURCHANG_TEST_WEB_URL','FOURCHANG_TEST_ADMIN_PASSWORD','FOURCHANG_TEST_OUTPUT')
$saved=@{};foreach($key in $keys){$saved[$key]=[Environment]::GetEnvironmentVariable($key)}
$webProcess=$null
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Push-Location $root
try {
    # Cả Web và WinForms cùng gọi API database tạm của runner cha, không dùng dữ liệu khách thật.
    $env:FOURCHANG_API_URL=$BaseUrl
    $env:FOURCHANG_TEST_API_URL=$BaseUrl
    $env:FOURCHANG_TEST_PROXY_URL="http://localhost:$ProxyPort/"
    $env:FOURCHANG_TEST_WEB_URL="http://localhost:$WebPort"
    $env:FOURCHANG_TEST_ADMIN_PASSWORD=$settings['ADMIN_INITIAL_PASSWORD']
    $env:FOURCHANG_TEST_OUTPUT=$OutputDirectory
    $project=Join-Path $PSScriptRoot 'WinForms/CustomersUiTests.csproj'
    & dotnet build $project --nologo
    if($LASTEXITCODE -ne 0){throw 'Build test WinForms thất bại.'}
    & dotnet (Join-Path $PSScriptRoot 'WinForms/bin/Debug/net8.0-windows/CustomersUiTests.dll') $OutputDirectory
    if($LASTEXITCODE -ne 0){throw 'Test WinForms thất bại.'}
    $webProcess=Start-Process -FilePath dotnet -ArgumentList @('bin/Debug/net8.0/FourChang.Web.dll','--urls',$env:FOURCHANG_TEST_WEB_URL) -WorkingDirectory (Join-Path $root 'src/FourChang.Web') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $OutputDirectory 'web.stdout.log') -RedirectStandardError (Join-Path $OutputDirectory 'web.stderr.log')
    $ready=$false
    for($i=0;$i -lt 30;$i++){
        try{if((Invoke-WebRequest ($env:FOURCHANG_TEST_WEB_URL+'/Account/Login') -TimeoutSec 2).StatusCode -eq 200){$ready=$true;break}}catch{}
        Start-Sleep -Milliseconds 300
    }
    if(-not $ready){throw 'Web kiểm thử chưa sẵn sàng.'}
    & node (Join-Path $PSScriptRoot 'Web/temporary-password.test.cjs')
    if($LASTEXITCODE -ne 0){throw 'Test Web thất bại. Kiểm tra Node.js/Playwright theo tests/README.md.'}
} finally {
    if($webProcess -and -not $webProcess.HasExited){Stop-Process -Id $webProcess.Id -Force;$webProcess.WaitForExit()}
    foreach($entry in $saved.GetEnumerator()){[Environment]::SetEnvironmentVariable($entry.Key,$entry.Value)}
    Pop-Location
}
