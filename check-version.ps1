# Check Stub DLL Version Script
# Usage: .\check-version.ps1

$stubDll = "bin\x86\Release\NewGMHack.Stub.dll"
$versionFile = "version.txt"

if (Test-Path $stubDll) {
    $version = [Reflection.AssemblyName]::GetAssemblyName($stubDll).Version
    Write-Host "Stub DLL Version: $version" -ForegroundColor Cyan
    
    # Update version.txt
    $version.ToString() | Set-Content $versionFile -NoNewline
    Write-Host "Updated $versionFile" -ForegroundColor Green
} else {
    Write-Host "Stub DLL not found at: $stubDll" -ForegroundColor Red
    Write-Host "Run build-release.ps1 first" -ForegroundColor Yellow
}

# Show current version.txt content
if (Test-Path $versionFile) {
    $current = Get-Content $versionFile -Raw
    Write-Host "`nversion.txt content: $current" -ForegroundColor Gray
}
