# Branch Protection Setup Guide (GitHub UI Method)

**Easiest method - 2 minutes setup**

## Step 1: Protect master Branch

1. **Open branch settings:**
   https://github.com/MVSharp/NewGMHack/settings/branches

2. **Click "Add branch protection rule"**

3. **Configure master branch:**
   ```
   Branch name pattern: master

   ✅ Require a pull request before merging
     ✓ Require approvals: 1
     ✓ Dismiss stale reviews when new commits are pushed

   ✅ Require status checks to pass before merging
     ✓ Require branches to be up to date before merging

   ✅ Do not allow bypassing the above settings
     Select: MVSharp (you)

   ✅ Restrict who can push to matching branches
     Select: MVSharp (you)

   ❌ Allow force pushes: UNCHECKED (block force pushes)

   ❌ Allow deletions: UNCHECKED (block deletions)
   ```

4. **Click "Create"**
5. **Click "Enable automatic rebase"** (if prompted)

## Step 2: Protect dev Branch

1. **Click "Add branch protection rule"** (again)

2. **Configure dev branch:**
   ```
   Branch name pattern: dev

   ✅ Require a pull request before merging
     ✓ Require approvals: 0 (or leave blank)

   ✅ Require status checks to pass before merging
     ✓ Require branches to be up to date before merging

   ✅ Do not allow bypassing the above settings
     Select: MVSharp (you)

   ✅ Restrict who can push to matching branches
     Select: MVSharp (you)

   ❌ Allow force pushes: UNCHECKED (block force pushes)

   ❌ Allow deletions: UNCHECKED (block deletions)
   ```

3. **Click "Create"**

## Step 3: Enable Auto-Delete (Optional)

1. **Go to repository settings:**
   https://github.com/MVSharp/NewGMHack/settings

2. **Scroll to "Buttons" section**

3. **Check: "Automatically delete head branches"**

4. **Scroll to bottom, click "Save changes"**

## Verification

After setup, verify:

```bash
# Check master protection
gh api repos/MVSharp/NewGMHack/branches/master/protection

# Check dev protection
gh api repos/MVSharp/NewGMHack/branches/dev/protection
```

Expected output (should show protection settings, not "Branch not protected")

## What This Achieves

### master branch:
- ✅ Only MVSharp can push
- ✅ Must create PR (requires 1 approval)
- ✅ CI must pass before merge
- ✅ No force pushes allowed
- ✅ No deletion allowed
- ✅ Linear history enforced

### dev branch:
- ✅ Only MVSharp can push
- ✅ Must create PR (no approval required for WIP)
- ✅ No force pushes allowed
- ✅ No deletion allowed

## After Protection

Your workflow becomes:
1. Create feature branch from dev
2. Make changes, commit
3. Create PR: feat/* → dev
4. Merge to dev (no approval needed)
5. When ready, create PR: dev → master
6. Auto-release happens! 🚀

---

**Time: 2-3 minutes**
**Difficulty: Easy**
**Impact: High security** 🔒
