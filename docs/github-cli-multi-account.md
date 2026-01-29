# GitHub CLI Multi-Account Setup Guide

## Current Git Configuration ✅

You already have Git configured correctly for multiple accounts:

**Global Config** (`~/.gitconfig`):
```ini
[includeIf "gitdir:~/source/repos/MVSharp"]
    path = ~/MVSharpGit/.gitconfig

[includeIf "gitdir:~/source/repos/MichaelVanHouHei"]
    path = ~/MichaelVanHouHeiGit/.gitconfig
```

**Account A** (`~/MVSharpGit/.gitconfig`):
- Name: MVSharp
- Email: zxcnio93@gmail.com

**Account B** (`~/MichaelVanHouHeiGit/.gitconfig`):
- Name: MichaelVanHouHei
- Email: lai28hei@qq.com

## Setting Up GitHub CLI for Multiple Accounts

GitHub CLI (gh) doesn't support `includeIf` natively, but we can achieve the same behavior using credential helpers.

### Option 1: Use Git Credential Helper (RECOMMENDED)

#### Step 1: Generate Personal Access Tokens

1. **Account A (MVSharp)**: https://github.com/settings/tokens
   - Generate token with `repo` and `workflow` scopes
   - Save the token

2. **Account B (MichaelVanHouHei)**: https://github.com/settings/tokens
   - Generate token with `repo` and `workflow` scopes
   - Save the token

#### Step 2: Configure Credential Helpers

Add to your global `~/.gitconfig`:

```ini
[credential "https://github.com"]
    useHttpPath = true
    helper = manager-core
```

Then configure git to use different credentials per repository:

**For MVSharp repos:**
```bash
cd ~/source/repos/MVSharp/NewGMHack/NewGMHack
git config credential.https://github.com.username MVSharp
```

**For MichaelVanHouHei repos:**
```bash
cd ~/source/repos/MichaelVanHouHei/your-repo
git config credential.https://github.com.username MichaelVanHouHei
```

#### Step 3: Configure GitHub CLI OAuth

Authenticate with both accounts (GitHub CLI supports multiple auth tokens):

```bash
# Login for MVSharp account (default)
gh auth login

# Then for specific repos, override:
cd ~/source/repos/MichaelVanHouHei/your-repo
gh auth switch
```

### Option 2: Use Aliases (SIMPLEST)

Create shell aliases for each account:

**PowerShell** (`$PROFILE`):
```powershell
# Account A (MVSharp)
function gh-mvsharp {
    $env:GH_TOKEN = "ghp_YOUR_MVSHARP_TOKEN"
    gh @args
}

# Account B (MichaelVanHouHei)
function gh-michaelvanhouhei {
    $env:GH_TOKEN = "ghp_YOUR_MICHAELVANHOUHEI_TOKEN"
    gh @args
}

# Default to MVSharp
Set-Alias gh gh-mvsharp
```

**Bash/Zsh** (`~/.bashrc` or `~/.zshrc`):
```bash
# Account A (MVSharp)
alias gh-mvsharp='GH_TOKEN="ghp_YOUR_MVSHARP_TOKEN" gh'

# Account B (MichaelVanHouHei)
alias gh-michaelvanhouhei='GH_TOKEN="ghp_YOUR_MICHAELVANHOUHEI_TOKEN" gh'

# Default to MVSharp
alias gh=gh-mvsharp
```

### Option 3: Per-Repository Configuration (BEST)

Configure gh per repository:

```bash
# In MVSharp repo
cd ~/source/repos/MVSharp/NewGMHack/NewGMHack
gh auth login
# Select GitHub.com → HTTPS → Login as MVSharp → Store token

# In MichaelVanHouHei repo
cd ~/source/repos/MichaelVanHouHei/your-repo
gh auth login
# Select GitHub.com → HTTPS → Login as MichaelVanHouHei → Store token
```

GitHub CLI will store the token in the repository's local git config:

```ini
# In MVSharp repo
[gh]
    token = ghp_MVSHARP_TOKEN

# In MichaelVanHouHei repo
[gh]
    token = ghp_MICHAELVANHOUHEI_TOKEN
```

## Recommended Approach

**Use Option 3 (Per-Repository Configuration)** because:
1. ✅ Most reliable
2. ✅ Works automatically once set up
3. ✅ Matches your existing git includeIf pattern
4. ✅ No aliases or environment variables needed

## Quick Setup Commands

```bash
# MVSharp account (current repo)
cd ~/source/repos/MVSharp/NewGMHack/NewGMHack
gh auth login
# Follow prompts, login as MVSharp (zxcnio93@gmail.com)

# MichaelVanHouHei account
cd ~/source/repos/MichaelVanHouHei/some-repo
gh auth login
# Follow prompts, login as MichaelVanHouHei (lai28hei@qq.com)

# Verify which account gh is using
gh auth status
```

## Managing PRs with gh (Multi-Account)

Once configured, `gh` will automatically use the correct account based on the repository:

```bash
# In MVSharp repo (uses MVSharp account)
cd ~/source/repos/MVSharp/NewGMHack/NewGMHack
gh pr list
gh pr create

# In MichaelVanHouHei repo (uses MichaelVanHouHei account)
cd ~/source/repos/MichaelVanHouHei/some-repo
gh pr list
gh pr create
```

## Troubleshooting

### Wrong account being used?

```bash
# Check current auth
gh auth status

# Re-authenticate for this repo
gh auth logout
gh auth login
```

### Need to switch accounts temporarily?

```bash
# Use environment variable (one-time)
GH_TOKEN="ghp_OTHER_ACCOUNT_TOKEN" gh pr list
```

## Verification

After setup, verify in each repo:

```bash
# MVSharp repo
cd ~/source/repos/MVSharp/NewGMHack/NewGMHack
gh auth status
# Should show: Logged in as MVSharp (zxcnio93@gmail.com)

# MichaelVanHouHei repo
cd ~/source/repos/MichaelVanHouHei/your-repo
gh auth status
# Should show: Logged in as MichaelVanHouHei (lai28hei@qq.com)
```

---

**Setup Time: 5 minutes**
**Maintenance: Zero** (works automatically after setup)
