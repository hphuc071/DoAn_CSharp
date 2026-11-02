#Requires -Version 7.0
param([int]$Port=5183,[switch]$KeepDatabase,[switch]$IncludePresentation,[switch]$IncludeRegression)
# Tương thích lệnh cũ; không sao chép logic kiểm thử tại scripts/.
& (Join-Path $PSScriptRoot '../tests/QuanLy/UC-ADM-01/Test-Api.ps1') @PSBoundParameters
