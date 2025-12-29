$ErrorActionPreference = "Stop"

# ==========================================
# Build Script for NewGMHack
# Run with: .\build-release.ps1
# ==========================================

# 1. Build Frontend
Write-Host "Building Frontend..." -ForegroundColor Cyan
Push-Location frontend
try {
    pnpm install
    # Clean output
    if (Test-Path dist) { Remove-Item dist -Recurse -Force }
    pnpm build
    if (-not (Test-Path dist/index.html)) { throw "Build failed: dist/index.html not found" }
}
finally {
    Pop-Location
}

# 2. Copy Assets to GUI wwwroot
Write-Host "Copying Assets to NewGmHack.GUI/wwwroot..." -ForegroundColor Cyan
$guiWwwRoot = Join-Path "NewGmHack.GUI" "wwwroot"

# Clean Destination (Keep directory but empty it)
if (Test-Path $guiWwwRoot) { 
    Get-ChildItem $guiWwwRoot | Remove-Item -Recurse -Force 
} else {
    New-Item -ItemType Directory -Path $guiWwwRoot | Out-Null
}

# Copy built files
Copy-Item -Path "frontend/dist/*" -Destination $guiWwwRoot -Recurse -Force
if (-not (Test-Path "$guiWwwRoot/index.html")) { throw "Copy failed: index.html not found in wwwroot" }

# 3. Clean .NET Projects
Write-Host "Cleaning .NET Projects..." -ForegroundColor Cyan
dotnet clean "NewGmHack.GUI/NewGmHack.GUI.csproj" -p:Platform=x86 -v q
dotnet clean "NewGMHack.Stub/NewGMHack.Stub.csproj" -p:Platform=x86 -v q

# 4. Build .NET Projects (x86 Release)
Write-Host "Building .NET Projects (x86)..." -ForegroundColor Cyan

# Build GUI (Main App)
dotnet build "NewGmHack.GUI/NewGmHack.GUI.csproj" -c Release -p:Platform=x86
if ($LASTEXITCODE -ne 0) { throw "GUI build failed" }

# Build Stub (Injected DLL)
dotnet build "NewGMHack.Stub/NewGMHack.Stub.csproj" -c Release -p:Platform=x86
if ($LASTEXITCODE -ne 0) { throw "Stub build failed" }

# 4.5. Update version.txt with Stub DLL version
Write-Host "Updating version.txt..." -ForegroundColor Cyan
$stubDll = "bin\x86\Release\NewGMHack.Stub.dll"
if (Test-Path $stubDll) {
    $version = [Reflection.AssemblyName]::GetAssemblyName($stubDll).Version.ToString()
    $version | Set-Content "version.txt" -NoNewline
    Write-Host "  Version: $version" -ForegroundColor Green
} else {
    Write-Host "  Warning: Stub DLL not found, version.txt not updated" -ForegroundColor Yellow
}

# 5. Cleanup Temp Files
Write-Host "Cleaning up temp files..." -ForegroundColor Cyan

# Remove frontend dist (already copied to wwwroot)
if (Test-Path "frontend/dist") { Remove-Item "frontend/dist" -Recurse -Force }

# Remove old_frontend folder if exists (from git restore testing)
if (Test-Path "old_frontend") { Remove-Item "old_frontend" -Recurse -Force }

# Remove obj folders (intermediate build files)
Get-ChildItem -Path . -Recurse -Directory -Filter "obj" | ForEach-Object {
    Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Build Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Executable located at:" -ForegroundColor Yellow
Write-Host "  NewGmHack.GUI\bin\x86\Release\net10.0-windows7.0\NewGmHack.GUI.exe" -ForegroundColor White

# ==========================================
# Visual Studio Integration Notes:
# ==========================================
# To auto-build frontend when debugging in Visual Studio:
#
# 1. Right-click NewGmHack.GUI project > Properties
# 2. Go to "Build Events" tab
# 3. Add Pre-build event:
#    cd $(SolutionDir)frontend && pnpm build && xcopy /E /I /Y dist $(ProjectDir)wwwroot
#
# Or add to .csproj file:
# <Target Name="BuildFrontend" BeforeTargets="Build">
#   <Exec Command="cd frontend &amp;&amp; pnpm build" WorkingDirectory="$(SolutionDir)" />
#   <ItemGroup>
#     <FrontendFiles Include="$(SolutionDir)frontend\dist\**\*" />
#   </ItemGroup>
#   <Copy SourceFiles="@(FrontendFiles)" DestinationFolder="$(ProjectDir)wwwroot\%(RecursiveDir)" />
# </Target>
# ==========================================
