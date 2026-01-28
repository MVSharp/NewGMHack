# GitHub CLI Quick Reference

## Multi-Account Setup

**MVSharp Account** (zxcnio93@gmail.com)
- Repos: `~/source/repos/MVSharp/*`
- Already configured ✅

**MichaelVanHouHei Account** (lai28hei@qq.com)
- Repos: `~/source/repos/MichaelVanHouHei/*`
- Run `gh auth login` in one of these repos to set up

## Current Repo: NewGMHack (MVSharp Account)

### Active Pull Request
```bash
# View PR
gh pr view 6

# Check CI status
gh pr checks 6

# Merge when ready
gh pr merge 6

# Add labels
gh pr edit 6 --add-label "WIP"
gh pr edit 6 --remove-label "WIP"
```

### Common Commands

**Pull Requests:**
```bash
gh pr list                          # List all PRs
gh pr view <number>                 # View PR details
gh pr create                        # Create PR (interactive)
gh pr create --base dev --title "X" # Create with flags
gh pr merge <number>                # Merge PR
gh pr close <number>                # Close PR
```

**Issues:**
```bash
gh issue list                       # List issues
gh issue view <number>              # View issue
gh issue create                     # Create issue (interactive)
```

**Branches:**
```bash
gh repo view                        # Show repo overview
git branch -vv                      # Show branches with tracking
```

**Actions/CI:**
```bash
gh run list                         # List workflow runs
gh run view <id>                    # View run details
gh run watch <id>                   # Watch run in real-time
gh run rerun <id>                   # Re-run failed workflow
```

**Authentication:**
```bash
gh auth status                      # Check which account is active
gh auth logout                      # Logout from current repo
gh auth login                       # Login to account
```

## Daily Workflow

### Working on Feature
```bash
# 1. Start from dev
git checkout dev
git pull origin dev

# 2. Create feature branch
git checkout -b feat/my-feature

# 3. Make changes and commit
git add .
git commit -m "feat: my feature"

# 4. Push to remote
git push origin feat/my-feature

# 5. Create PR
gh pr create --base dev --title "feat: my feature"

# 6. Check status
gh pr view <number>
gh pr checks <number>

# 7. When CI passes and reviewed
gh pr merge <number>
```

### Reviewing PRs
```bash
# List open PRs
gh pr list --state open

# View PR details
gh pr view <number>

# View PR diff
gh pr diff <number>

# Checkout PR locally
gh pr checkout <number>

# Approve PR
gh pr review <number> --approve

# Request changes
gh pr review <number> --body "Please fix X"

# Merge PR
gh pr merge <number>
```

### Checking CI Status
```bash
# List recent runs
gh run list --limit 10

# View run details
gh run view <id>

# Watch run in real-time
gh run watch <id>

# View failed logs
gh run view <id> --log

# Re-run failed job
gh run rerun <id>
```

## Tips & Tricks

### Quick PR Creation from Branch
```bash
# Auto-detect base branch and create PR
gh pr create --fill

# Fill from commit messages
gh pr create --fill --body "Auto-generated from commits"
```

### Bulk Operations
```bash
# Close all merged PRs
gh pr list --state merged --json number --jq '.[].number' | xargs -I {} gh pr close {}

# Add label to all PRs from specific user
gh pr list --author username --json number --jq '.[].number' | xargs -I {} gh pr edit {} --add-label "triage"
```

### View Actions
```bash
# View last failed run
gh run list --status failure --limit 1 --json databaseId --jq '.[0].databaseId' | xargs -I {} gh run view {}

# Watch current workflow
gh run watch --interval 5
```

## Account Switching

### Check Current Account
```bash
gh auth status
```

### Switch Accounts in Different Repos
```bash
# MVSharp repo
cd ~/source/repos/MVSharp/NewGMHack/NewGMHack
gh auth status  # Shows MVSharp

# MichaelVanHouHei repo
cd ~/source/repos/MichaelVanHouHei/some-repo
gh auth status  # Shows MichaelVanHouHei (after setup)
```

### Re-authenticate if Needed
```bash
gh auth logout
gh auth login
```

## Aliases (Optional)

Add to `~/.bashrc` or PowerShell profile:

```bash
# Quick status
alias gs='gh auth status && git status'

# Quick PR view
alias pr='gh pr view $(git branch --show-current)'

# Quick CI check
alias ci='gh run list --limit 5'
```

## Troubleshooting

### "Not logged in" error
```bash
gh auth login
```

### Wrong account being used
```bash
gh auth logout
gh auth login
```

### PR checks failing
```bash
gh pr checks <number>
gh run view <id> --log-failed
```

### Can't push to branch
```bash
# Check branch permissions
gh repo view --json defaultBranchRef

# Check if protected
gh api repos/MVSharp/NewGMHack/branches/master/protection
```

## Current Repo Stats

```bash
# Repo info
gh repo view

# Open PRs
gh pr list

# Open issues
gh issue list

# Recent activity
gh repo view --json name,owner,description,defaultBranchRef
```

---

**Last Updated:** 2026-01-28
**Current PR:** #6 - feat: camera-relative FreeMove implementation (WIP)
