# Security Policy

## Supported Versions

Currently supported versions with security updates:

| Version | Supported          |
|---------|--------------------|
| Latest  | :white_check_mark: |

## Reporting a Vulnerability

### Scope

This security policy covers:
- The NewGMHack software code
- Build and release infrastructure
- CI/CD workflows
- Dependencies with known vulnerabilities

### What to Report

Please report:
- Security vulnerabilities in the code
- Malicious code injection risks
- Data exposure issues
- Dependency vulnerabilities
- Build/CI security issues

### What NOT to Report

Please do NOT report:
- Game detection/ban issues (use GitHub Issues)
- Feature requests (use GitHub Issues)
- Bug reports (use GitHub Issues)
- Questions about game anti-cheat (use Discussions)

### How to Report

**Private Disclosure (Preferred):**
1. Send an email to: zxcnio93@gmail.com
2. Include "Security: NewGMHack" in the subject
3. Provide details:
   - Vulnerability description
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if known)

**Response Timeline:**
- Initial response: Within 48 hours
- Investigation: Within 1 week
- Fix/patch: As needed, based on severity
- Public disclosure: After fix is released

### Security Best Practices

**For Users:**
- Only download from official GitHub releases
- Verify file checksums (provided in releases)
- Keep your version updated
- Use at your own risk (this is game modification software)

**For Developers:**
- Follow secure coding practices
- Review dependencies for vulnerabilities
- Test changes in isolated environment
- Never commit sensitive data (tokens, keys, etc.)

## Dependency Management

This project uses:
- [.NET 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20](https://nodejs.org/)
- Various NuGet and npm packages

Dependencies are monitored via:
- Dependabot alerts (if enabled)
- Manual security reviews
- Regular dependency updates

## Build Security

**Release Process:**
1. All code changes go through Pull Requests
2. CI/CD builds and tests all changes
3. Releases are created automatically via protected branch workflow
4. All release files are signed with SHA256 checksums

**Checksum Verification:**
```bash
# Windows
CertUtil -hashfile NewGMHack.GUI.exe SHA256
CertUtil -hashfile NewGMHack.Stub.dll SHA256

# Linux/Mac
shasum -a 256 NewGMHack.GUI.exe
shasum -a 256 NewGMHack.Stub.dll
```

Compare with checksums.txt from the release.

## Risk Disclosure

**Important:** This is game modification software. Risks include:

- **Game Account Risks:**
  - Account suspension/ban
  - Loss of game progress
  - Violation of game ToS

- **System Risks:**
  - Requires injecting code into game process
  - May conflict with anti-cheat software
  - Run at your own risk

- **Privacy Risks:**
  - Reads game memory
  - Modifies game process
  - Does NOT collect personal data

**Use this software responsibly and at your own risk.**

## Security Updates

**Update Process:**
1. Security fixes are prioritized
2. New releases published via GitHub Releases
3. Update via built-in auto-update (if enabled)
4. Check release notes for security fixes

**Monitoring:**
- GitHub Security Advisories
- Dependabot alerts
- Community reports

## Contact

**Security Contact:** zxcnio93@gmail.com
**GitHub Issues:** https://github.com/MVSharp/NewGMHack/issues
**Discussions:** https://github.com/MVSharp/NewGMHack/discussions

---

**Last Updated:** 2026-01-28
