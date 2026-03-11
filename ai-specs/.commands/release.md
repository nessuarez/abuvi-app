# Release: bump version, create PR & GitHub release

## Arguments
- Version bump type: $ARGUMENTS (major | minor | patch — default: minor)

## Process

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

- Analyze `git log main..dev` to generate an accurate summary of all changes.
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
