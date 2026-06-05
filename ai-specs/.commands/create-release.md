# Release: bump version, create PR & GitHub release

## Arguments

- Version bump type: $ARGUMENTS (major | minor | patch — default: minor)

## Process

### 0. Sync local repository

Before doing anything else, fetch the latest state from the remote so all comparisons are accurate:

```bash
git fetch --tags
git fetch origin dev main
git checkout dev
git pull origin dev
```

### 1. Determine new version

- Read `frontend/package.json` to get the current version.
- Apply semver bump based on the argument provided (major, minor, or patch). Default to **minor** if no argument is given.
- Confirm the new version with the user before proceeding.

### 2. Bump version in package.json

- Update the `"version"` field in `frontend/package.json` to the new version.
- Commit the change: `chore: bump version to vX.Y.Z`
- Push to the current branch.

### 3. Create PR (dev → main)

- Use `gh pr create --base main --head dev`.
- Follow the PR template from `.github/PULL_REQUEST_TEMPLATE.md`:

```
## What changed
<1-2 sentence summary of all changes in dev since last release>

## Why
<Why this release is needed>

## Type of change
<Check applicable boxes>

## Testing
<Summary of what was tested>
```

- To get the accurate list of changes, use the **last tag** as reference (not `main`):

  ```bash
  LAST_TAG=$(git describe --tags --abbrev=0 origin/dev^)
  git log $LAST_TAG..origin/dev --oneline
  ```

  This avoids including changes from previous releases when `main` hasn't been merged recently.
- Add a label `vX.Y.Z` to the PR (create the label if it doesn't exist, color `#0E8A16`).

### 4. Create GitHub Release (draft)

- Use `gh release create vX.Y.Z --target main --draft --title "vX.Y.Z"`.
- Generate release notes from the commit history, grouped by category:
  - **Funcionalidades**: new features
  - **Mejoras UX**: UI/UX improvements
  - **Fixes**: bug fixes
  - **Otros**: refactors, docs, chores

### 5. Summary

Print a summary with links to:

- The PR
- The draft release
- The new version number
