#!/usr/bin/env pwsh
# MCP Test Server Runner Script

param(
    [switch]$Build = $false,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Write-Host "MCP Test Server Runner" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan
Write-Host ""

# Build if requested
if ($Build) {
    Write-Host "Building test server..." -ForegroundColor Yellow
    dotnet build -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Build succeeded!" -ForegroundColor Green
    Write-Host ""
}

# Run the server
$serverPath = "bin\$Configuration\net10.0\mcp-test-server.exe"
if (-not (Test-Path $serverPath)) {
    Write-Host "Server executable not found at: $serverPath" -ForegroundColor Red
    Write-Host "Run with -Build flag to build first" -ForegroundColor Yellow
    exit 1
}

Write-Host "Starting MCP Test Server..." -ForegroundColor Green
Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
Write-Host ""

& $serverPath

