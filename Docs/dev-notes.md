# Sam n' Ella development notes

Active development lives in [SamElla](../SamElla/README.md), a .NET pixel-art platformer with a native Windows game window and editable text levels. [NetMan-0.1](../Legacy/NetMan-0.1/README.md) preserves the original console version.

![](screen-shot.png)

From this repository folder, run:

```powershell
dotnet run --project .\SamElla\SamElla.csproj
```

See the game README for controls, editable text levels, and development checks.

## Build a release

From the repository root, run:

```powershell
.\build.ps1
```

Project and output paths resolve relative to the script, so it also works when invoked by absolute path from another working directory.

To publish and launch the game, double-click `build-exec.bat` or run:

```powershell
.\build-exec.bat
```

This runs the same Release build, tests, Native AOT publish, and ZIP packaging, then opens the newly published game. If any step fails, the game is not launched. Build options are forwarded, for example `.\build-exec.bat -SkipTests`. The PowerShell equivalent is `.\build.ps1 -Run`.

It builds Release, runs the MSTest suite, and publishes a self-contained, trimmed Native AOT Windows x64 executable optimized for size. It then creates `Publish/SamElla-vX.Y.Z/` and `Publish/SamElla-vX.Y.Z.zip`. The ZIP contains that versioned directory with `SamElla.exe`, runtime assets, editable levels, and the license. No .NET installation is needed on the player's machine.

The name comes from the first three components of `FileVersion` in the root `GameVersion.props` (Major.Minor.Revision), imported by both desktop and MAUI projects. Change that property to change the package version. Rebuilding the same version replaces its generated folder and ZIP after publishing and compression succeed; older versions are retained. `Publish` is ignored by Git.

Requirements: Windows, PowerShell 5.1 or later, the .NET 10 SDK, and Visual Studio 2022 or later with **Desktop development with C++**, including its default MSVC and Windows SDK components. The first Native AOT build also restores compiler/runtime packages from NuGet. See Microsoft's [Native AOT prerequisites](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).

Optional arguments:

```powershell
.\build.ps1 -SkipTests
.\build.ps1 -Runtime win-arm64
```

ARM64 publishing needs the corresponding C++ ARM64 build tools. Both architectures use the requested version-only ZIP name, so publishing another architecture replaces the same version's previous output.

To reduce size, tests live in a separate project, and the distribution excludes PNG-export tooling, preview images, asset documentation, and debug symbols. Runtime `.sprite` files, the palette, and levels remain editable. Publishing favors size over speed, uses invariant globalization, and removes runtime stack traces, debugger support, diagnostic events, and full framework exception messages. Normal development builds retain their existing behavior. See Microsoft's [AOT optimization](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing) and [trimming options](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options).

## Tests

`SamElla.Tests` uses MSTest.Sdk with Microsoft.Testing.Platform. From the repository root:

```powershell
dotnet test --solution .\SamElla.slnx
dotnet test --project .\SamElla.Tests\SamElla.Tests.csproj --configuration Release
```

Open `SamElla.slnx` in Visual Studio to discover and run the tests in Test Explorer. Tests cover gameplay, collisions, reward and animation timing, both splatter effects, asset validation, and rendering without opening a game window. Each scenario has its own temporary level files, so tests can run independently and in parallel.

`global.json` selects the .NET 10 SDK family and the MTP runner. The first test run restores Microsoft's test packages from NuGet. These packages belong only to the test project and are not included in the game or release ZIP. `build.ps1` (and `build.bat`) runs Release tests before publishing and stops if any fail; `-SkipTests` explicitly bypasses them.


## Automatic build versions

`.github/workflows/increment-develop-build.yml` increments the fourth version component after a pull request is merged into `develop`. For example, `0.4.0.60` becomes `0.4.0.61`. The workflow updates `FileVersion` and `AssemblyVersion` together in the root `GameVersion.props`, increments its independent `MobileBuildNumber`, and commits the change back to `develop` as `github-actions[bot]`. The first three components remain under your control. Opening, updating, or reopening a PR assigns a matching CI-only Windows/mobile version through `.github/workflows/mobile.yml`; it does not commit a version change. Closing a PR without merging and directly pushing a normal commit do not change the committed version.

Each version commit records the PR number and merge commit. Rerunning a completed workflow does not increment again. Simultaneous merges use normal pushes with retries against the latest `develop`, so both merges receive an increment; the workflow never force-pushes. After a successful run, pull the latest `develop` to use its updated version locally. ZIP names keep their existing three-part format, such as `SamElla-v0.4.0.zip`.

Enable GitHub Actions for the repository and allow this workflow's `contents: write` permission. The repository's branch rules must permit the automation identity to push the version commit to `develop`. If your rules require all changes through PRs, signed commits, or restricted push identities, configure an approved automation identity and its credentials rather than disabling those rules. The workflow uses the repository's built-in `GITHUB_TOKEN` by default. If your approved automation account needs a different token, store it as the repository secret `VERSION_BUMP_TOKEN`; checkout will use that token instead. Signing requirements need a signing step configured for the automation identity. The workflow does not change repository permissions or branch rules. A rejected push fails with an explanation and can be rerun after the repository policy is configured.

PRs from branches in this repository use the `pull_request` merged/closed event. Fork and Dependabot PRs use `pull_request_target` because ordinary fork PR tokens are read-only. GitHub loads target-event workflows from the repository's **default branch**, so also include this workflow on that branch when enabling fork/Dependabot support (`master` if that is your default branch). Both routes require an already-merged PR targeting `develop`; only the trusted, merged `develop` checkout is used, never the unmerged PR head. The PR marker protects against duplicate processing. See GitHub's [PR event documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows) and [token permission documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#permissions).

At build number `65534`, automation stops instead of overflowing the assembly-version limit. Manually advance the release prefix and reset the fourth component in both version properties before the next merge. Keep those properties synchronized and do not manually update the build component in ordinary feature PRs. Never reset `MobileBuildNumber`: Android store codes must remain increasing across release-prefix changes. CI increments also fail explicitly before exceeding an assembly or Android limit. See [mobile versioning](mobile.md) for the PR-only numbering and preview application ID.

To validate the automation locally with PowerShell 7 and Git:

```powershell
pwsh -NoProfile -File .github/scripts/Test-BuildVersion.ps1
```

This creates disposable repositories under the ignored `output/VersioningChecks` folder. It tests increments, reruns, simultaneous merges, formatting preservation, dirty checkout rejection, unmerged commits, overflow, release-prefix changes, and server-side write rejection without contacting GitHub or modifying the current checkout.
