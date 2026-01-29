# Repository Structure Analysis & CI/CD Recommendations

**Date:** 2026-01-28
**Repository:** MVSharp/NewGMHack

## Current State ✅

### Branch Structure

```
master (c62be69)
  └── Merge PR #5 from dev
      └── dev (dc51f3b)
          ├── feat/camera-relative-freemove (71e486f) ← NEW
          ├── feat/optimize-recv
          ├── feat/performance-ops
          ├── feat/MigrateNewHookAndFixNewVersionPacket
          └── feat/EspHack
```

### Status Summary

✅ **Branch workflow is CORRECT:**
- `master` = stable releases (latest: c62be69)
- `dev` = latest development changes (15 commits ahead locally, need push)
- `feat/*` = feature branches (5 active branches)

⚠️ **Issue Found:**
- **Local dev is 15 commits ahead of remote dev**
- Remote dev hasn't been updated with the freemove work
- This is why your new `feat/camera-relative-freemove` branch appears to have diverged

### Current CI/CD Setup

**File:** `.github/workflows/release.yml`

**Current Triggers:**
```yaml
on:
  push:
    tags:
      - 'v*'          # Triggers on version tags
  workflow_dispatch:   # Manual trigger
```

**What it does:**
- Builds frontend, GUI, and Stub
- Creates release packages
- Generates SHA256 checksums
- Creates GitHub releases with auto-update XML

## 🔧 Required Changes

### 1. Update dev Branch on Remote

Your local `dev` branch has 15 commits that aren't on remote. You need to push them:

```bash
git checkout dev
git push origin dev
```

This will ensure `feat/camera-relative-freemove` PR has the correct base.

### 2. CI/CD Workflow Changes

**Current problem:** Workflow triggers on ANY tag push, not specifically when dev→master.

**Recommended changes:**

#### Option A: Add PR-based workflow (RECOMMENDED)

Create `.github/workflows/pr-master.yml`:
```yaml
name: PR to Master

on:
  pull_request:
    branches:
      - master
    types: [closed, opened, synchronize]

jobs:
  validate-and-release:
    if: github.event.pull_request.merged == true && github.head_ref == 'dev'
    runs-on: windows-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Validate branch source
        run: |
          if ("${{ github.event.pull_request.head.ref }}" -ne "dev") {
            Write-Error "PRs to master must come from dev branch"
            exit 1
          }

      - name: Build and Test
        run: |
          # Add build/test commands here

      - name: Create version tag
        run: |
          # Auto-tag on merge from dev to master
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git tag -a "v$(date +%Y.%m.%d)" -m "Release $(date +%Y-%m-%d)"
          git push origin "v$(date +%Y.%m.%d)"
```

#### Option B: Branch Protection Rules (SIMPLER)

Set up GitHub branch protection:
1. Go to repository Settings → Branches
2. Add rule for `master` branch:
   - ✅ Require pull request before merging
   - ✅ Require approvals (1)
   - ✅ Disallow direct pushes
   - ✅ Restrict who can push (only you)
3. Allow only `dev` branch to merge to `master`

### 3. Update Release Workflow

Modify `.github/workflows/release.yml`:

```yaml
on:
  push:
    tags:
      - 'v*'
    branches:
      - master    # Only build releases on master
  workflow_dispatch:
```

Or add validation to ensure it only runs on master:
```yaml
jobs:
  build:
    if: github.ref == 'refs/heads/master' || startsWith(github.ref, 'refs/tags/')
```

## 📋 Recommended Workflow

### Development Workflow

```
1. Create feature branch from dev
   git checkout dev
   git pull origin dev
   git checkout -b feat/your-feature

2. Make changes and commit
   git add .
   git commit -m "feat: your feature"

3. Push and create PR to dev
   git push origin feat/your-feature
   # Create PR on GitHub: feat/your-feature → dev

4. After review, merge to dev
   # Merge PR on GitHub (or use git)

5. When feature(s) are stable, create PR: dev → master

6. After merging dev to master:
   - CI/CD triggers automatically
   - Builds release
   - Creates GitHub release
   - Auto-generates version tag
```

### Branch Rules

| Branch | Purpose | Protection | CI/CD |
|--------|---------|------------|-------|
| `master` | Stable releases | 🔒 PR required, no direct push | ✅ Full build + release |
| `dev` | Latest development | 🔒 PR required, no direct push | ✅ Build only (no release) |
| `feat/*` | Feature development | ⚪ No protection | ❌ No CI/CD |

## 🚀 Immediate Action Items

### Priority 1: Push local dev to remote

```bash
git checkout dev
git push origin dev
```

### Priority 2: Merge feature branch to dev

```bash
# After PR review, merge feat/camera-relative-freemove to dev
# Either via GitHub PR or:
git checkout dev
git merge feat/camera-relative-freemove
git push origin dev
```

### Priority 3: Set up CI/CD for dev→master PRs

1. Create `.github/workflows/pr-master.yml` (see Option A above)
2. OR set up branch protection rules (see Option B above)
3. Update `release.yml` to only run on master branch

### Priority 4: Clean up old feature branches

```bash
# List and delete merged feature branches
git branch --merged | grep "feat/" | xargs git branch -d

# Force delete old unmerged branches (if no longer needed)
git push origin --delete feat/EspHack feat/MigrateNewHookAndFixNewVersionPacket
```

## 📊 Visual Workflow

```
┌─────────────────┐
│  feat/feature   │ ← Development work
└────────┬────────┘
         │ PR
         ▼
┌─────────────────┐
│      dev        │ ← Integration branch (latest changes)
└────────┬────────┘
         │ PR (when stable)
         ▼
┌─────────────────┐
│    master       │ ← Stable releases (CI/CD triggers here)
└─────────────────┘
         │ Tag push (v*)
         ▼
┌─────────────────┐
│  GitHub Release │ ← Automated release creation
└─────────────────┘
```

## ✅ Verification Checklist

After implementing these changes:

- [ ] Local dev pushed to remote
- [ ] `feat/camera-relative-freemove` PR created/merged to dev
- [ ] CI/CD workflow only triggers on PR to master (from dev)
- [ ] Branch protection rules enabled for master
- [ ] Old feature branches cleaned up
- [ ] Documentation updated (CONTRIBUTING.md)

## 🔍 Additional Recommendations

### 1. Add CONTRIBUTING.md

```markdown
# Contributing to NewGMHack

## Branch Strategy

- `master` - Stable releases
- `dev` - Latest development (merge feat/* here)
- `feat/*` - Feature branches (create from dev)

## Workflow

1. Create feature branch from `dev`
2. Make changes and commit
3. Create PR to `dev`
4. After review and merge, create PR `dev` → `master` for release
```

### 2. Add Branch Naming Convention

- `feat/` - New features
- `fix/` - Bug fixes
- `refactor/` - Code refactoring
- `docs/` - Documentation changes
- `perf/` - Performance improvements

### 3. Automate Version Bumping

Consider using `GitVersion` or similar tool to auto-generate version numbers based on commit history.

---

**Generated:** 2026-01-28
**Status:** Ready for implementation
