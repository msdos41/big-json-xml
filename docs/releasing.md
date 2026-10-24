# Releasing

A release is an annotated tag on `main`. Pushing that tag runs `.github/workflows/release.yml`, which creates the GitHub Release. The Release is not created by hand.

The product asset is one Windows executable. The local publish file stays `BigJsonXml.exe`. The names below are the GitHub Release assets.

## Rules

1. The version is SemVer: `MAJOR.MINOR.PATCH`. A pre-release adds a hyphen and dot-separated identifiers, for example `0.2.0-beta.1`. The tag is the version with a leading `v`: `v0.2.0` or `v0.2.0-beta.1`.
2. The tag is annotated. Its message is `Release X.Y.Z`. A lightweight tag fails the workflow.
3. The tagged commit contains the workflow, and its `src/BigJsonXml.Desktop/BigJsonXml.Desktop.csproj` has `<Version>X.Y.Z</Version>` for that same version, including any pre-release suffix.
4. `CHANGELOG.md` has this as its first version section. `YYYY-MM-DD` is the calendar day of the tag:

```markdown
## [X.Y.Z] - YYYY-MM-DD
```

5. A tag is pushed once and stays on that commit.
6. The workflow uploads two assets. `X.Y.Z` includes a pre-release suffix when the tag has one:

```text
BigJsonXml-X.Y.Z-windows-amd64.exe
BigJsonXml-X.Y.Z-checksums.sha256
```

The checksum file is one line: the lowercase SHA-256, two spaces, and the exe name. A tag whose version contains `-` is marked pre-release and does not become Latest.

## Steps

Run these from the repository root on `main`, in order. Replace `X.Y.Z` with the version.

1. Edit `CHANGELOG.md`. Put the new section above the older ones:

```markdown
## [X.Y.Z] - YYYY-MM-DD
```

Use the calendar day you will create the tag.

2. Set the same version in `src/BigJsonXml.Desktop/BigJsonXml.Desktop.csproj`:

```xml
<Version>X.Y.Z</Version>
```

3. Run the tests:

```powershell
dotnet test BigJsonXml.slnx
```

4. Commit the changelog, the project version, and any other files that belong in this release. The commit message follows the repository commit rules.

5. Confirm the worktree is clean, then create the annotated tag on that commit:

```powershell
git status --porcelain
git tag -a vX.Y.Z -m "Release X.Y.Z"
git show vX.Y.Z
```

`git show` prints a tagger, a date, and the message `Release X.Y.Z`. That is an annotated tag.

6. Push the branch, then push that one tag:

```powershell
git push origin main
git push origin vX.Y.Z
```

Push this tag by name. Do not push every local tag.

## What the workflow runs

After the tag push, `.github/workflows/release.yml` does this in order:

1. Rejects a tag that is not `vMAJOR.MINOR.PATCH` or `vMAJOR.MINOR.PATCH-prerelease`.
2. Requires the dated changelog section, requires it to be first, and requires the desktop `<Version>` to match.
3. Rejects a lightweight tag.
4. Runs `dotnet test BigJsonXml.slnx`.
5. Writes the release notes from that changelog section.
6. Publishes `BigJsonXml-X.Y.Z-windows-amd64.exe` and `BigJsonXml-X.Y.Z-checksums.sha256`.
7. Creates the GitHub Release. A pre-release tag gets `--prerelease`.

## First tag

`0.1.0` is the version in the desktop project and in `CHANGELOG.md` (`## [0.1.0] - 2026-10-22`). Tag it with these commands when that date is still the tag day. If the tag day is later, change the date in `CHANGELOG.md` and commit that change before tagging.

```powershell
git status --porcelain
git tag -a v0.1.0 -m "Release 0.1.0"
git show v0.1.0
git push origin main
git push origin v0.1.0
```

The Release assets are `BigJsonXml-0.1.0-windows-amd64.exe` and `BigJsonXml-0.1.0-checksums.sha256`.

## Pre-release

Use the same steps. For a beta of `0.2.0` the version is `0.2.0-beta.1`, the heading is `## [0.2.0-beta.1] - YYYY-MM-DD`, and the tag command is:

```powershell
git tag -a v0.2.0-beta.1 -m "Release 0.2.0-beta.1"
git push origin v0.2.0-beta.1
```

The assets are `BigJsonXml-0.2.0-beta.1-windows-amd64.exe` and `BigJsonXml-0.2.0-beta.1-checksums.sha256`. The Release is marked pre-release. Latest remains the newest stable tag.
