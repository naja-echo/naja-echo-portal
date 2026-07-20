# Contributing to Naja Echo Portal

Thank you for your interest in contributing! This document outlines the process for setting up your development environment and submitting changes.

## Development Setup

### Prerequisites
- **.NET 10.0** or later
- **Node.js 22** or later
- **PostgreSQL** (for local database)
- **Docker & Docker Compose** (optional, for containerized environment)

### Initial Setup

1. Clone the repository:
```bash
git clone https://github.com/naja-echo/naja-echo-portal.git
cd naja-echo-portal
```

2. Install git hooks for local safeguards:
```bash
./setup-hooks.sh
```

3. Set up backend:
```bash
cd backend
dotnet restore NajaEcho.slnx
dotnet tool restore
cd ..
```

4. Set up frontend:
```bash
cd frontend
npm ci
cd ..
```

## Code Quality Standards

### Backend (C#)
- **StyleCop Rules**: Enforced via `StyleCopAnalyzers`
- **Code Analysis**: Enforced via `Microsoft.CodeAnalysis.NetAnalyzers`
- **Configuration**: `.editorconfig` at repository root
- **Tests Required**: Unit tests for new features/bug fixes

### Frontend (TypeScript/React)
- **Linting**: ESLint with TypeScript support
- **Code Style**: Configured in `eslint.config.js`
- **Tests Required**: Unit tests with Vitest

## Pre-Commit Checks

When you commit code, the following checks run automatically:

- ✅ Frontend linting (if frontend files changed)
- ✅ Backend code analysis (if backend files changed)

If checks fail, fix the issues and retry your commit. To skip checks (not recommended):
```bash
git commit --no-verify
```

## Development Workflow

### 1. Create a Feature Branch
```bash
git checkout -b feature/your-feature-name
```

### 2. Make Your Changes

**Backend Changes:**
```bash
cd backend
dotnet build NajaEcho.slnx
dotnet test NajaEcho.slnx
```

**Frontend Changes:**
```bash
cd frontend
npm run dev          # Start dev server
npm run lint         # Check for linting issues
npm run test:run     # Run tests
```

### 3. Commit Your Changes
Ensure your changes pass pre-commit checks:
```bash
git add .
git commit -m "Brief description of changes"
```

### 4. Push to GitHub
```bash
git push origin feature/your-feature-name
```

### 5. Create a Pull Request
- Push your branch to GitHub
- Open a PR against the `main` branch
- Fill out the PR template
- Address any feedback from code review

## Pull Request Requirements

All PRs to `main` must:

- ✅ Have at least **1 code review approval**
- ✅ Pass **all CI/CD checks** (backend build, frontend lint, tests)
- ✅ Have code coverage from tests
- ✅ Be up-to-date with main branch

## Merging to Production

Only designated maintainers can merge PRs to `main`. After merge, the following happens automatically:

1. **CI/CD Pipeline** runs (tests, build, analysis)
2. **Docker Image** is built and published to GHCR
3. **Production Deployment** occurs (if all checks pass)

## Code Review Guidelines

When reviewing PRs:

- Focus on correctness and maintainability
- Check that code follows the style guidelines
- Ensure tests cover the changes
- Suggest improvements, not just issues
- Approve once you're confident in the quality

## Questions or Issues?

- Check the [README](README.md) for project overview
- Open a GitHub issue for bug reports or feature requests
- Start a discussion in GitHub Discussions for questions

## License

By contributing to this project, you agree that your contributions will be licensed under the project's license.
