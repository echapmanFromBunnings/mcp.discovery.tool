#!/usr/bin/env pwsh
# View MCP Metadata Script

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$metadataPath = "bin\$Configuration\net10.0\mcp-metadata\mcp-metadata.json"

if (-not (Test-Path $metadataPath)) {
    Write-Host "Metadata file not found at: $metadataPath" -ForegroundColor Red
    Write-Host "Build the project first: dotnet build -c $Configuration" -ForegroundColor Yellow
    exit 1
}

Write-Host "MCP Metadata Viewer" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan
Write-Host ""

$metadata = Get-Content $metadataPath | ConvertFrom-Json

Write-Host "Generated At: " -NoNewline
Write-Host $metadata.GeneratedAtUtc -ForegroundColor Green
Write-Host ""

foreach ($assembly in $metadata.Assemblies) {
    Write-Host "Assembly: " -NoNewline -ForegroundColor Yellow
    Write-Host $assembly.AssemblyPath
    Write-Host ""
    
    foreach ($class in $assembly.Classes) {
        Write-Host "  [$($class.Kind)] " -NoNewline -ForegroundColor Cyan
        Write-Host $class.TypeName -ForegroundColor White
        
        if ($class.Description) {
            Write-Host "    Description: $($class.Description)" -ForegroundColor Gray
        }
        
        if ($class.Audiences -and $class.Audiences.Count -gt 0) {
            Write-Host "    Audiences: $($class.Audiences -join ', ')" -ForegroundColor Gray
        }
        
        foreach ($member in $class.Members) {
            Write-Host "    [$($member.Kind)] " -NoNewline -ForegroundColor Magenta
            Write-Host "$($member.Name) " -NoNewline -ForegroundColor White
            
            if ($member.Title) {
                Write-Host "- $($member.Title)" -NoNewline -ForegroundColor Gray
            }
            Write-Host ""
            
            if ($member.Description) {
                Write-Host "      Description: $($member.Description)" -ForegroundColor DarkGray
            }
            
            if ($member.Audiences -and $member.Audiences.Count -gt 0) {
                Write-Host "      Audiences: $($member.Audiences -join ', ')" -ForegroundColor DarkGray
            }
        }
        
        Write-Host ""
    }
}

Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "--------" -ForegroundColor Cyan
$totalClasses = ($metadata.Assemblies | ForEach-Object { $_.Classes.Count } | Measure-Object -Sum).Sum
$totalMembers = ($metadata.Assemblies | ForEach-Object { $_.Classes | ForEach-Object { $_.Members.Count } } | Measure-Object -Sum).Sum
Write-Host "Total Classes: $totalClasses"
Write-Host "Total Members: $totalMembers"

