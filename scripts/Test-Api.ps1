#Requires -Version 7.0
param([string]$BaseUrl='http://localhost:5181')
& (Join-Path $PSScriptRoot '../tests/TichHop/Test-Api.ps1') @PSBoundParameters
