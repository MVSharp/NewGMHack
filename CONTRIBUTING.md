# Contributing to NewGMHack

Thanks for your interest in contributing! This document outlines the development workflow and guidelines.

## Table of Contents
- [Code of Conduct](#code-of-conduct)
- [Development Workflow](#development-workflow)
- [Branch Strategy](#branch-strategy)
- [Pull Request Process](#pull-request-process)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Commit Messages](#commit-messages)

## Code of Conduct

### Our Pledge
- Be respectful and inclusive
- Focus on constructive feedback
- Accept responsibility and apologize if needed
- Collaborate openly

### Unacceptable Behavior
- Harassment or discrimination
- Personal attacks
- Public or private harassment
- Publishing private information

## Development Workflow

### Prerequisites
- .NET 10.0 SDK
- Node.js 20
- pnpm (for frontend)
- Git
- GitHub CLI (gh)

### Getting Started

1. **Fork and Clone**
   ```bash
   git clone https://github.com/YOUR_USERNAME/NewGMHack.git
   cd NewGMHack
   ```

2. **Install Dependencies**
   ```bash
   # Frontend
   cd frontend
   pnpm install
   pnpm build

   # .NET Projects
   dotnet restore
   dotnet build
   ```

3. **Run Development**
   - GUI: `dotnet run --project NewGmHack.GUI/NewGmHack.GUI.csproj`
   - Frontend dev: `cd frontend && pnpm dev`

## Branch Strategy

```
master (stable releases)
  ↑
dev (latest development)
  ↑
feat/* (feature branches)
fix/* (bug fixes)
refactor/* (refactoring)
docs/* (documentation)
perf/* (performance improvements)
```

### Branch Rules

| Branch  | Purpose                | Protected | CI/CD  |
|---------|------------------------|-----------|--------|
| master  | Stable releases        | ✅ Yes    | Full   |
| dev     | Latest development     | ✅ Yes    | Build  |
| feat/*  | New features           | ❌ No     | None   |
| fix/*   | Bug fixes              | ❌ No     | None   |

### Branch Naming

- `feat/feature-name` - New features
- `fix/bug-description` - Bug fixes
- `refactor/component-name` - Code refactoring
- `docs/documentation-topic` - Documentation changes
- `perf/performance-area` - Performance improvements
- `ci/cd-improvement` - CI/CD changes
- `test/testing-area` - Test improvements

## Pull Request Process

### 1. Create Feature Branch
```bash
git checkout dev
git pull origin dev
git checkout -b feat/your-feature
```

### 2. Make Changes
```bash
# Make your changes
git add .
git commit -m "feat: your feature description"
```

### 3. Push and Create PR
```bash
git push origin feat/your-feature
gh pr create --base dev --title "feat: your feature"
```

### 4. PR Checklist
- [ ] Code compiles without errors
- [ ] CI/CD builds pass
- [ ] Tests pass (if applicable)
- [ ] Documentation updated (if needed)
- [ ] Commit messages follow conventions
- [ ] No sensitive data included

### 5. Review and Merge
- Address review feedback
- Update branch as needed
- Merge when approved

### 6. Cleanup
```bash
# Delete merged branch locally
git branch -d feat/your-feature

# Delete merged branch remotely
gh repo delete --yes MVSharp/NewGMHack:feat/your-feature
# OR
git push origin --delete feat/your-feature
```

## Coding Standards

### C# Code Style
- Follow C# naming conventions
- Use XML documentation for public APIs
- Keep methods focused and small
- Use meaningful variable names
- Add comments for complex logic

**Example:**
```csharp
/// <summary>
/// Scans player entity and extracts position and view matrix.
/// </summary>
/// <returns>Tuple of position, view matrix, and success flag.</returns>
private (Vector3 Position, Matrix ViewMatrix, bool IsValid) ScanMySelf()
{
    // Implementation
}
```

### Vue/TypeScript Code Style
- Use Composition API
- Follow Vue 3 style guide
- TypeScript strict mode
- Meaningful component names
- Props validation

**Example:**
```typescript
// Use meaningful names
const { isLoading, data } = useFetchPlayerData()

// Add JSDoc for complex functions
/**
 * Calculates camera-relative movement vector
 * @param direction - Forward/Back/Left/Right
 * @param viewMatrix - Camera view matrix
 * @returns Normalized movement vector
 */
function calculateMovement(direction: Direction, viewMatrix: Matrix): Vector3 {
  // Implementation
}
```

### Memory Safety
- Always validate memory reads
- Use safe pointer traversal
- Handle exceptions appropriately
- No hardcoded memory addresses

## Testing Guidelines

### Unit Tests
- Test critical logic
- Mock external dependencies
- Test edge cases
- Aim for >70% coverage on core logic

### Integration Tests
- Test memory reading/writing
- Test overlay rendering
- Test IPC communication

### Manual Testing
- Test in actual game environment
- Verify feature functionality
- Check for performance issues
- Test error scenarios

## Commit Messages

### Format
```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types
- `feat` - New feature
- `fix` - Bug fix
- `docs` - Documentation changes
- `style` - Code style changes (formatting)
- `refactor` - Code refactoring
- `perf` - Performance improvements
- `test` - Adding or updating tests
- `ci` - CI/CD changes
- `chore` - Maintenance tasks

### Examples

**Good:**
```
feat(overlay): add player distance filtering

Display only enemies within 100m range on radar.
Improves performance and reduces clutter.

Closes #123
```

```
fix(freemove): correct coordinate transformation

Use quadrant detection for View Matrix vectors.
Fixes movement issues in camera directions 2 and 4.

Fixes #456
```

**Bad:**
```
update
fixed bug
stuff
```

### Multi-Line Commit Example
```
feat(aimbot): add priority targeting system

- Target lowest HP enemies first
- Add distance weighting
- Configurable priority rules

Testing:
- Verified in 5 game sessions
- Performance: <5% CPU usage
- Memory: No leaks detected

Co-Authored-By: Contributor Name <email@example.com>
```

## Build and Release

### Building Locally
```bash
# Full build
.\build-release.ps1

# Individual components
cd frontend && pnpm build
dotnet build NewGmHack.GUI/NewGmHack.GUI.csproj -c Release -p:Platform=x86
dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86
```

### Creating Releases
1. Merge `dev` → `master` via PR
2. CI/CD auto-creates version tag
3. GitHub release auto-generated
4. Assets packaged automatically

**Manual Release (if needed):**
```bash
# Tag release
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0

# CI/CD will build and create release
```

## Questions?

- **GitHub Issues:** https://github.com/MVSharp/NewGMHack/issues
- **Discussions:** https://github.com/MVSharp/NewGMHack/discussions
- **Email:** zxcnio93@gmail.com

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

**Happy Coding!** 🎮
