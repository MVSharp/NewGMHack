# NewGMHack Repository Settings Analysis

**Date:** 2026-01-28
**Repository:** https://github.com/MVSharp/NewGMHack
**Default Branch:** master

## ✅ What's Already Configured Well

### CI/CD Workflows ✅
- ✅ `.github/workflows/ci-dev.yml` - CI for dev branch
- ✅ `.github/workflows/pr-master.yml` - Automated releases on dev→master merge
- ✅ `.github/workflows/release.yml` - Manual tag-based releases
- ✅ **Auto-tagging:** YES ✅ - PR workflow will auto-create version tags (v1.0.x.x)
- ✅ **Auto-release:** YES ✅ - Creates GitHub releases with all assets

### Branch Structure ✅
- ✅ `master` - Stable releases
- ✅ `dev` - Latest development
- ✅ `feat/*` - Feature branches
- ✅ Proper workflow: feat/* → dev → master

### Git Configuration ✅
- ✅ Multi-account Git setup (MVSharp vs MichaelVanHouHei)
- ✅ Proper `.gitignore`
- ✅ Branch tracking configured

### Documentation ✅ (in docs/)
- ✅ Repository structure analysis
- ✅ CI/CD setup summary
- ✅ GitHub CLI multi-account guide
- ✅ GitHub CLI cheatsheet
- ✅ Project architecture (CLAUDE.md)

## ⚠️ What's Missing or Not Standard

### 1. Branch Protection ❌ CRITICAL
**Status:** `master` branch is **NOT protected**

**Current:**
```json
{
  "message": "Branch not protected"
}
```

**Risks:**
- ❌ Anyone can push directly to master (bypasses review)
- ❌ No required PR reviews
- ❌ No status checks required before merge
- ❌ CI/CD could be bypassed

**Recommended Settings for `master`:**
```yaml
✅ Require pull request before merging
  - Require approvals: 1
  - Dismiss stale reviews: yes

✅ Require status checks to pass before merging
  - CI - Dev Branch (must pass)
  - PR to Master (Release) (must pass)

✅ Require branches to be up to date before merging
  - Require branches to be up to date

✅ Restrict who can push to master
  - Only: MVSharp (you)
  - Bypass: Admins

❌ Do NOT allow:
  - Force pushes (blocked)
  - Deletions (blocked)
```

**Recommended Settings for `dev`:**
```yaml
✅ Require pull request before merging
  - Require approvals: 0 (optional for WIP)
  - Require status checks: CI - Dev Branch

✅ Restrict who can push
  - Only: MVSharp

❌ Do NOT allow:
  - Force pushes (blocked)
  - Deletions (blocked)
```

### 2. Standard Repository Files ⚠️

#### Missing Files:
```
❌ README.md (optional - you said you don't want one)
❌ LICENSE
❌ SECURITY.md
❌ CONTRIBUTING.md
❌ CODE_OF_CONDUCT.md (optional for solo projects)
❌ SUPPORT.md
❌ .github/ISSUE_TEMPLATE/
❌ .github/PULL_REQUEST_TEMPLATE.md
```

#### Recommended Minimum:
```
✅ LICENSE                    # REQUIRED for open-source
✅ SECURITY.md                # Security policy (especially for game mod)
✅ CONTRIBUTING.md            # Dev workflow, branch rules
```

#### Optional but Good to Have:
```
💡 .github/dependabot.yml     # Automated dependency updates
💡 .github/CODEOWNERS         # Code ownership rules
💡 .github/PULL_REQUEST_TEMPLATE.md  # PR template
```

### 3. Security Settings ⚠️

**Current:**
- ❌ No security policy (`securityPolicyUrl: ""`)
- ❌ No license (`licenseInfo: null`)
- ❌ Security features: disabled

**Recommended:**
```yaml
✅ Enable security features
✅ Add SECURITY.md (security policy URL)
✅ Add LICENSE (choose MIT, Apache-2.0, or GPL)
✅ Enable Dependabot alerts
✅ Enable secret scanning (if using API keys)
```

### 4. Repository Features ⚠️

**Current Settings:**
```json
{
  "hasIssuesEnabled": true,        ✅
  "hasWikiEnabled": true,          ✅
  "hasDiscussionsEnabled": false,  ✅
  "deleteBranchOnMerge": false,    ⚠️ Should be true
  "visibility": "PUBLIC"           ✅
}
```

**Recommended Changes:**
```yaml
✅ deleteBranchOnMerge: true
   - Auto-delete branches after PR merge
   - Keeps repo clean
```

### 5. Topics/Tags ❌
**Current:** No repository topics

**Recommended:**
```bash
gh repo edit --add-topic c-sharp
gh repo edit --add-topic dotnet
gh repo edit --add-topic game-mod
gh repo edit --add-topic game-hacking
gh repo edit --add-topic directx
gh repo edit --add-topic wpf
gh repo edit --add-topic vuejs
gh repo edit --add-topic cheat-detection
```

### 6. CI/CD Issues ⚠️

**Current Issue:** Latest CI run failed (PR #6)

**Error:**
```
error CS0234: The type or namespace name 'Debug' does not exist
in the namespace 'InjectDotnet'
```

**Impact:** PRs can't be merged until CI passes

**Fix Required:** Check InjectDotnet.csproj and fix conditional compilation for Debug namespace

### 7. Auto-Tag Verification ✅

**Question:** Will dev→master PR successfully auto-tag?

**Answer:** YES ✅

**Workflow:**
```yaml
# .github/workflows/pr-master.yml
on:
  pull_request:
    branches: [master]
    types: [closed]

jobs:
  create-release:
    if: github.event.pull_request.merged == true
    steps:
      - name: Create and push tag
        run: |
          $version = "${{ needs.get-version.outputs.version }}"
          $tagName = "v$version"
          git tag -a "$tagName" -m "Release $version"
          git push origin "$tagName"
```

**Verification:** ✅ Workflow has:
1. ✅ Trigger on PR to master when merged
2. ✅ Validation that PR source is dev
3. ✅ Auto-tag with version from Stub DLL
4. ✅ Auto-create GitHub release
5. ✅ Package all assets (GUI, Stub, wwwroot, checksums, update.xml)

## 📋 Priority Action Items

### Priority 1: CRITICAL - Branch Protection

**Action Required:** Enable branch protection for master and dev

**Option A: Use GitHub CLI (RECOMMENDED)**
```bash
# Protect master branch
gh api \
  --method PUT \
  -H "Accept: application/vnd.github+json" \
  repos/MVSharp/NewGMHack/branches/master/protection \
  -f required_pull_request_reviews='{"required_approving_review_count":1,"dismiss_stale_reviews":true,"require_code_owner_reviews":false}' \
  -f enforce_admins=true \
  -f required_linear_history='{"enabled":true}' \
  -f allow_force_deletes=false

# Protect dev branch (less strict)
gh api \
  --method PUT \
  -H "Accept: application/vnd.github+json" \
  repos/MVSharp/NewGMHack/branches/dev/protection \
  -f required_pull_request_reviews='{"required_approving_review_count":0}' \
  -f enforce_admins=true \
  -f required_linear_history='{"enabled":true}' \
  -f allow_force_deletes=false
```

**Option B: Use GitHub UI**
1. Go to: https://github.com/MVSharp/NewGMHack/settings/branches
2. Click "Add rule"
3. Branch name pattern: `master`
4. Enable settings as shown above
5. Repeat for `dev`

### Priority 2: HIGH - Fix CI Build Failure

**Action Required:** Fix InjectDotnet build error

**Quick Fix:**
```bash
# Check the issue
cd InjectDotnet
cat Injector.cs | head -20

# Likely fix: Add conditional compilation
#if DEBUG
    // Debug-specific code
#endif
```

### Priority 3: MEDIUM - Add Standard Files

**Action Required:** Create LICENSE, SECURITY.md, CONTRIBUTING.md

**Files to Create:**
1. `LICENSE` - MIT License (recommended)
2. `SECURITY.md` - Security policy for game mod
3. `CONTRIBUTING.md` - Development workflow

### Priority 4: LOW - Repository Settings

**Action Required:** Enable auto-delete and add topics

```bash
# Enable auto-delete merged branches
gh repo edit --delete-branch-on-merge

# Add topics
gh repo edit --add-topic c-sharp --add-topic dotnet --add-topic game-mod
```

## 📊 Repository Health Score

| Category | Status | Score |
|----------|--------|-------|
| CI/CD | ✅ Excellent | 9/10 |
| Branch Structure | ✅ Good | 8/10 |
| Branch Protection | ❌ Critical Missing | 0/10 |
| Documentation | ✅ Good (internal) | 7/10 |
| Standard Files | ⚠️ Incomplete | 3/10 |
| Security | ⚠️ Basic | 4/10 |
| Auto-Tagging | ✅ Working | 10/10 |

**Overall: 5.9/10** (Good foundation, needs protection and standard files)

## 🎯 Recommended Action Plan

### Step 1: Immediate Security (5 minutes)
```bash
# Protect master branch
gh api --method PUT \
  repos/MVSharp/NewGMHack/branches/master/protection \
  -f required_pull_request_reviews='{"required_approving_review_count":1,"dismiss_stale_reviews":true}' \
  -f enforce_admins=true \
  -f required_linear_history='{"enabled":true}' \
  -f allow_force_deletes=false
```

### Step 2: Fix CI Build (10 minutes)
- Fix InjectDotnet Debug namespace issue
- Verify CI passes on PR #6

### Step 3: Add Standard Files (15 minutes)
- Create LICENSE
- Create SECURITY.md
- Create CONTRIBUTING.md

### Step 4: Final Polish (5 minutes)
```bash
# Enable auto-delete
gh repo edit --delete-branch-on-merge

# Add topics
gh repo edit --add-topic c-sharp --add-topic dotnet \
  --add-topic game-mod --add-topic directx --add-topic wpf
```

## ✅ Verification Checklist

After implementing above:

- [ ] master branch protected (PR required, no direct push)
- [ ] dev branch protected (less strict)
- [ ] CI builds passing
- [ ] LICENSE file added
- [ ] SECURITY.md added
- [ ] CONTRIBUTING.md added
- [ ] Auto-delete enabled
- [ ] Repository topics added
- [ ] Test dev→master PR (verify auto-tag works)

## 🚀 After Setup

Once everything is configured, your workflow will be:

```bash
# 1. Create feature branch
git checkout dev
git pull origin dev
git checkout -b feat/my-feature

# 2. Make changes, commit, push
git add .
git commit -m "feat: my feature"
git push origin feat/my-feature

# 3. Create PR to dev (GitHub enforces this)
gh pr create --base dev

# 4. After review, merge to dev
gh pr merge <number>

# 5. When ready for release, create PR: dev → master
gh pr create --base master

# 6. Merge to master
gh pr merge <number>

# 7. Automatic release happens:
#    - CI runs
#    - Version tag created (v1.0.x.x)
#    - GitHub release created
#    - Assets published
```

---

**Next Step:** Run the branch protection commands above (Priority 1)
