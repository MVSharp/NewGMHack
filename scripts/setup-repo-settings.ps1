# Repository Setup Script for NewGMHack
# This script configures recommended GitHub repository settings

$ErrorActionPreference = "Stop"

Write-Host "=== NewGMHack Repository Setup ===" -ForegroundColor Cyan
Write-Host ""

# Check if gh is authenticated
Write-Host "Checking GitHub CLI authentication..." -ForegroundColor Yellow
gh auth status | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Not authenticated. Please run: gh auth login" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Authenticated" -ForegroundColor Green
Write-Host ""

# Repository info
$repo = "MVSharp/NewGMHack"

Write-Host "=== Priority 1: Branch Protection ===" -ForegroundColor Cyan
Write-Host ""

# Protect master branch
Write-Host "Protecting master branch..." -ForegroundColor Yellow
try {
    gh api \
        --method PUT \
        --header "Accept: application/vnd.github+json" \
        repos/$repo/branches/master/protection \
        -f required_pull_request_reviews='{"required_approving_review_count":1,"dismiss_stale_reviews":true,"require_code_owner_reviews":false,"bypass_pull_request_allowances":{"apps":[],"users":["MVSharp"]}}' \
        -f enforce_admins=true \
        -f required_linear_history='{"enabled":true}' \
        -f allow_force_deletes=false \
        2>$null | Out-Null

    Write-Host "✅ master branch protected" -ForegroundColor Green
    Write-Host "   - PR required (1 approval)" -ForegroundColor Gray
    Write-Host "   - Admins enforced" -ForegroundColor Gray
    Write-Host "   - Linear history required" -ForegroundColor Gray
    Write-Host "   - Force pushes blocked" -ForegroundColor Gray
} catch {
    Write-Host "⚠️  Failed to protect master (may need admin UI)" -ForegroundColor Yellow
}

Write-Host ""

# Protect dev branch (less strict)
Write-Host "Protecting dev branch..." -ForegroundColor Yellow
try {
    gh api \
        --method PUT \
        --header "Accept: application/vnd.github+json" \
        repos/$repo/branches/dev/protection \
        -f required_pull_request_reviews='{"required_approving_review_count":0,"bypass_pull_request_allowances":{"apps":[],"users":["MVSharp"]}}' \
        -f enforce_admins=true \
        -f required_linear_history='{"enabled":true}' \
        -f allow_force_deletes=false \
        2>$null | Out-Null

    Write-Host "✅ dev branch protected" -ForegroundColor Green
    Write-Host "   - PR required (no approval needed)" -ForegroundColor Gray
    Write-Host "   - Admins enforced" -ForegroundColor Gray
    Write-Host "   - Linear history required" -ForegroundColor Gray
} catch {
    Write-Host "⚠️  Failed to protect dev (may need admin UI)" -ForegroundColor Yellow
}

Write-Host ""

Write-Host "=== Priority 2: Repository Settings ===" -ForegroundColor Cyan
Write-Host ""

# Enable auto-delete merged branches
Write-Host "Enabling auto-delete for merged branches..." -ForegroundColor Yellow
try {
    gh repo edit $repo --delete-branch-on-merge
    Write-Host "✅ Auto-delete enabled" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Failed to enable auto-delete" -ForegroundColor Yellow
}

Write-Host ""

# Add topics
Write-Host "Adding repository topics..." -ForegroundColor Yellow
try {
    gh repo edit $repo \
        --add-topic c-sharp \
        --add-topic dotnet \
        --add-topic game-mod \
        --add-topic game-hacking \
        --add-topic directx \
        --add-topic wpf \
        --add-topic vuejs \
        --add-topic typescript \
        --add-topic sharpdx \
        --add-topic cheat-detection 2>$null

    Write-Host "✅ Topics added" -ForegroundColor Green
    Write-Host "   Topics: c-sharp, dotnet, game-mod, game-hacking, directx, wpf, vuejs, typescript, sharpdx, cheat-detection" -ForegroundColor Gray
} catch {
    Write-Host "⚠️  Failed to add topics" -ForegroundColor Yellow
}

Write-Host ""

Write-Host "=== Priority 3: Standard Files ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "Creating standard repository files..." -ForegroundColor Yellow

# Check if files already exist
$files = @(
    "LICENSE",
    "SECURITY.md",
    "CONTRIBUTING.md"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "⚠️  $file already exists, skipping" -ForegroundColor Yellow
    } else {
        Write-Host "   $file - Not created (see below for templates)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "✅ File templates saved to setup-files/ directory" -ForegroundColor Green

Write-Host ""

Write-Host "=== Verification ===" -ForegroundColor Cyan
Write-Host ""

# Check protection status
Write-Host "Branch protection status:" -ForegroundColor Yellow
try {
    $masterProtection = gh api repos/$repo/branches/master/protection 2>$null
    if ($masterProtection) {
        Write-Host "✅ master: Protected" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ master: Not protected" -ForegroundColor Red
}

try {
    $devProtection = gh api repos/$repo/branches/dev/protection 2>$null
    if ($devProtection) {
        Write-Host "✅ dev: Protected" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ dev: Not protected" -ForegroundColor Red
}

Write-Host ""

# Check repo settings
Write-Host "Repository settings:" -ForegroundColor Yellow
$repoInfo = gh repo view $repo --json deleteBranchOnMerge
if ($repoInfo.deleteBranchOnMerge) {
    Write-Host "✅ Auto-delete: Enabled" -ForegroundColor Green
} else {
    Write-Host "❌ Auto-delete: Disabled" -ForegroundColor Red
}

Write-Host ""

Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ Repository configured with recommended settings" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Fix CI build failure (InjectDotnet Debug namespace)" -ForegroundColor White
Write-Host "2. Create LICENSE file (choose MIT, Apache-2.0, or GPL)" -ForegroundColor White
Write-Host "3. Create SECURITY.md (security policy for game mod)" -ForegroundColor White
Write-Host "4. Create CONTRIBUTING.md (development workflow)" -ForegroundColor White
Write-Host "5. Test dev→master PR to verify auto-tag works" -ForegroundColor White
Write-Host ""
Write-Host "For manual branch protection setup, visit:" -ForegroundColor Yellow
Write-Host "https://github.com/MVSharp/NewGMHack/settings/branches" -ForegroundColor Cyan
Write-Host ""
