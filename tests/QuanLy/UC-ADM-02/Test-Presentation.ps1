#Requires -Version 7.0
param([Parameter(Mandatory)][string]$BaseUrl,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot '../../DungChung/TestSupport.psm1') -Force
$root=Get-TestProjectRoot -StartPath $PSScriptRoot
$settings=Get-TestLocalSettings -ProjectRoot $root
$keys=@('FOURCHANG_API_URL','FOURCHANG_TEST_ADMIN_PASSWORD');$saved=@{}
foreach($key in $keys){$saved[$key]=[Environment]::GetEnvironmentVariable($key)}
try{
    $env:FOURCHANG_API_URL=$BaseUrl;$env:FOURCHANG_TEST_ADMIN_PASSWORD=$settings['ADMIN_INITIAL_PASSWORD']
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    & dotnet build (Join-Path $PSScriptRoot 'WinForms/AccessUiTests.csproj') --nologo
    if($LASTEXITCODE -ne 0){throw 'Build test giao diện UC-ADM-02 thất bại.'}
    & dotnet (Join-Path $PSScriptRoot 'WinForms/bin/Debug/net8.0-windows/AccessUiTests.dll') $OutputDirectory
    if($LASTEXITCODE -ne 0){throw 'Test giao diện UC-ADM-02 thất bại.'}
}finally{foreach($entry in $saved.GetEnumerator()){[Environment]::SetEnvironmentVariable($entry.Key,$entry.Value)}}
