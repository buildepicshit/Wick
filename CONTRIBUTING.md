# Contributing to Wick

Thank you for your interest in contributing to Wick. This file is the public contributor onboarding guide and the project’s source for engineering standards, pull-request workflow, code style, and hard rules.

## Getting Started

### Prerequisites

- **.NET 10 SDK** — the exact SDK version is pinned to `10.0.201` in `global.json` with `rollForward: latestFeature`, meaning older 10.0.x SDKs will not satisfy the pin and you will see a confusing "A compatible .NET SDK was not found" error. Install `10.0.201` or a newer 10.0.x from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0).
- A code editor — VS Code, Visual Studio, Rider, or any editor with C# LSP support.
- [Godot 4.6.1-stable-mono](https://godotengine.org/) for integration testing against the editor bridge (see [`STATUS.md`](STATUS.md) for current test state).
- `git` with worktree support (any modern version).

### Setup

```bash
git clone https://github.com/buildepicshit/Wick.git
cd Wick
dotnet build Wick.slnx
dotnet test Wick.slnx
```

A clean build should produce **zero warnings and zero errors**, and `dotnet test` should report all tests passing with zero skipped. If you see anything else on a fresh clone, please file an issue.

### Canonical verification command

```bash
dotnet build Wick.slnx --configuration Release && dotnet test Wick.slnx --configuration Release
```

This command must stay green commit-to-commit on every branch. If your PR turns it red, it isn't ready.

## Where to Read Next

| If you want to... | Read... |
|---|---|
| Understand engineering standards, PR workflow, worktree discipline, code style, hard rules | This file |
| See current project state, phase, recent work, blockers | [`STATUS.md`](STATUS.md) |
| Get the project overview and positioning | [`README.md`](README.md) |
| See release history | [`CHANGELOG.md`](CHANGELOG.md) |
| Understand licensing and GoPeak attribution | [`ATTRIBUTION.md`](ATTRIBUTION.md) |

## Quick Reference: Submitting a Pull Request

The short version:

1. **Open an issue first** for significant changes — discuss scope before coding.
2. **Work in a sibling worktree**, not on `main` directly:
   ```bash
   git fetch origin
   git worktree add ../Wick-worktrees/<branch-slug> -b <type>/<short-desc> origin/main
   ```
3. **Write the failing test first** (TDD is enforced).
4. **Implement minimally** to make the test pass.
5. **Run the canonical verification command** — zero warnings, zero failing tests.
6. **Commit using [Conventional Commits](https://www.conventionalcommits.org/)** (e.g. `fix(godot): handle null scene tree response`).
7. **Push and open a PR** with fresh verification output in the description.
8. **Wait for CI** to turn green, then request code review.
9. **Squash merge** once approved.

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). By participating, you agree to uphold this standard.

## Questions and Bug Reports

- **Questions, ideas, general conversation:** [GitHub Discussions](https://github.com/buildepicshit/Wick/discussions)
- **Bug reports and feature requests:** use the [issue tracker](https://github.com/buildepicshit/Wick/issues). Issue templates for bug reports and feature requests are provided — please use them, and search existing issues before filing to avoid duplicates.
- **Security vulnerabilities:** see [`SECURITY.md`](SECURITY.md) for the disclosure process.
