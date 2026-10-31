# Sam n' Ella - _Brutal Pixel Art Game_

> In the words of Ivan Drago,\
> &nbsp; &nbsp;"_If he dies, he dies._"

![](Docs/Sam-and-Ella.png)

This aint ya mamma's side scroller. Sam n' Ella (_previously, NetMan_) is a pixel-art platformer with a native game window and editable Sprites, Text levels, and really bad uncle quotes. Written 100% in C# and not forgiving like modern "Mario" games. _Saves? NO! Surprises? YES!_

![](screen-shot.png)

![Player collecting an extra heart and exploding](Docs/Player-Explosion.gif)

![Graveyard Shift](Docs/Level-Graveyard.png)

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

The build also produces a Velopack installer and automatic-update packages in
`Publish/Velopack/win-x64/`. The .NET SDK restores the pinned local packaging tool;
no global Velopack installation is required. Use `-SkipInstaller` to produce only
the original Native AOT folder and ZIP. `-Runtime win-arm64` produces a separate
ARM64 update channel when the required native toolchain is installed.

Project and output paths resolve relative to the script, so it also works when invoked by absolute path from another working directory.

To publish and launch the game, double-click `build-exec.bat` or run:

```powershell
.\build-exec.bat
```

This runs the same Release build, tests, Native AOT publish, and ZIP packaging, then opens the newly published game. If any step fails, the game is not launched. Build options are forwarded, for example `.\build-exec.bat -SkipTests`. The PowerShell equivalent is `.\build.ps1 -Run`.

### Build offline

Run a normal release build while connected once to cache dependencies for the
target runtime and install the pinned Velopack tool. Keep the .NET 10 SDK,
Visual Studio C++ build tools, and NuGet package cache installed. Then use:

```powershell
.\build.bat -Offline
# Build and launch instead:
.\build-exec.bat -Offline
```

PowerShell also supports `.\build.ps1 -Offline`. This option builds, tests, and
packages using cached dependencies, with NuGet feeds, vulnerability auditing,
and Velopack update checks disabled. It can restore deleted `obj` files from the
cache. Missing packages fail without attempting a download; reconnect and run
the normal build for the same `-Runtime` to populate the cache. New dependencies
or a different SDK/runtime can require another connected build.

`-SkipTests`, `-SkipInstaller`, and `-Runtime` work with `-Offline`. Signing options
require the normal connected build because they contact a timestamp server.

## Install and automatically update

Install `SuessLabs.SamElla-win-x64-Setup.exe` from the project's
[GitHub Releases](https://github.com/SuessLabs/Sam-n-Ella-Game/releases).
Velopack-managed installations and its generated portable ZIP check that repository's
stable releases at launch. Updates download without blocking the game. If a download
finishes while the title screen is open, the app exits cleanly and restarts updated;
otherwise it installs when the player exits. A pending downloaded update can also
be applied automatically at the next launch. Offline play remains available, and
update failures are logged to `%LocalAppData%/SamElla/Logs/updates.log`.

The Velopack package ID is `SuessLabs.SamElla`. This is a new installation identity:
users of the former `SuessLabs.NetMan` package must install the new setup EXE once.
This release does not provide an automatic migration between those package IDs.
The desktop and Start menu shortcuts are named `Sam-Ella`.
User content, audio settings, and update logs now live under `%LocalAppData%/SamElla`.
To preserve previous custom content, close the game and copy the `Content`
folder from `%LocalAppData%/NetMan` into `%LocalAppData%/SamElla` before launching
the new version. Audio preferences are loaded at startup only from
`%LocalAppData%/SamElla/settings.txt` and saved when Settings closes or the game exits.

Settings includes **Opt-in Pre-Release (Experimental)**, off by default. Use
Left/Right to turn it off/on, or Enter/Space to toggle it. The saved preference
takes effect on the next launch: enabled includes public GitHub prereleases in
Velopack update checks; disabled checks stable releases only. Turning it off does
not downgrade an installed experimental build; it waits for a newer stable build.
Update downloads and installation follow the existing title-screen/exit behavior.
An update already downloaded in the current session follows the previous setting.

The original `SamElla-vX.Y.Z.zip` remains available as an unmanaged portable build;
it does not self-update. Existing ZIP users must install the setup EXE or use the
Velopack-generated portable ZIP once to enable automatic updates.

For each release, increment the project FileVersion and build, then upload **all files**
from `Publish/Velopack/win-x64/` as assets of the same GitHub Release. The full `.nupkg`
and `releases.win-x64.json` are required; uploading only a ZIP is insufficient.
The new `publish-release.yml` workflow performs the build, tests, and upload automatically
when a GitHub Release is published from a tag containing this integration. It also
attaches the original ZIP. The workflow uses GitHub's built-in release token; client
applications contain no GitHub credentials. Draft releases are never offered;
prereleases require the saved experimental opt-in. If using immutable GitHub
releases, build locally and attach the assets while the release is still a draft,
before publishing it.

### Automatic releases from merged PRs

| Merged into | Version | Tag | GitHub publication |
| --- | --- | --- | --- |
| `develop` | Increment the fourth component first | `v0.7.0.108-pre` | Pre-release |
| `master` | Use the version already promoted from develop | `v0.7.0.108` | Stable, marked Latest |

Merging a PR into `develop` now runs the existing build-version increment,
then builds and tests that exact versioned commit, checks the native installer
hook, and publishes a GitHub **Pre-release** with the installer, update feed,
packages, and standalone ZIP. Tags use `v<FileVersion>-pre`, for example
`v0.7.0.108-pre`. Closing an unmerged PR does not publish anything.

The tag is created only after the build succeeds. Assets upload to a draft
before publication, and these prereleases never replace the stable Latest
release. Retrying a failed run reuses its original versioned commit, resumes
an unfinished draft, and leaves an already published prerelease untouched.
Existing tags pointing at another commit are rejected rather than moved.

Merging into `master` runs the same build, tests, and packaging against the
exact master merge commit, then publishes a separate stable release marked
**Latest**. It does not increment the version again. Its tag has no `-pre`
suffix, and the earlier develop prerelease remains available. Publish each
stable version once; a different commit cannot reuse an existing stable tag.

These paths take effect when their workflows are merged into `develop` and
`master`, respectively;
previously merged PRs are not retroactively published. Keep the workflows on
the default branch (`master`) updated as well for fork/Dependabot PR events.
The existing `VERSION_BUMP_TOKEN` option still applies if branch protection
requires it. Release publication uses GitHub's built-in token with
`contents: write`; no additional release secret is required. The build workflow
is reused directly, since bot-created release events do not trigger another
workflow. Manually published releases keep their existing build/upload flow.

Validate the automation locally with PowerShell 7:

```powershell
pwsh -NoProfile -File .github/scripts/Test-BuildVersion.ps1
pwsh -NoProfile -File .github/scripts/Test-ReleasePublishing.ps1
```

These checks use disposable local Git repositories and a mocked GitHub CLI;
they do not push to GitHub or publish releases.

### Local self-signing

To create or reuse a local development signing certificate and sign a release:

```powershell
.\build.bat -SelfSign
```

This signs `SamElla.exe` and Velopack's generated executables during packaging,
before the package hashes and archives are finalized. `-SkipInstaller -SelfSign`
also signs the standalone executable. The Windows SDK's `signtool.exe` is required.
Signing or timestamp failures stop the build before existing release outputs are replaced.
If a game is running from the normal release folder, use a separate destination:
`.\build.bat -SelfSign -PublishDirectory .\Publish\SelfSigned`.

The certificate is stored in the current user's Personal certificate store. Its
private key is non-exportable and is never written into this repository. The same
certificate is reused on later builds while it remains valid. A public-only copy
is exported to ignored `output/Signing/SamElla-SelfSigned.cer`. No certificate is
automatically installed into a trusted certificate store.

Self-signing does **not** remove public SmartScreen or unknown-publisher warnings.
It is for development and controlled testing; users do not trust this certificate
by default. PowerShell may report `NotTrusted` or `UnknownError` for the untrusted root.
The build verifies the signature and file integrity with Windows WinVerifyTrust,
allowing only a successful check or the specific untrusted-root result. Do not distribute private signing keys or ask public users to
disable Windows security features.

To use an existing code-signing certificate from `Cert:\CurrentUser\My` instead:

```powershell
.\build.bat -SigningCertificateThumbprint YOUR_40_CHARACTER_THUMBPRINT
```

The default RFC 3161 timestamp server is `http://timestamp.digicert.com`; override
it with `-TimestampUrl`. Builds without signing arguments remain unsigned,
including the current GitHub release workflow. A local non-exportable private key
is not available on GitHub-hosted runners. SignPath integration will require a
separate CI signing configuration; it can replace this local signing option while
keeping `SuessLabs.SamElla` as the package ID.

The four-part Windows FileVersion is preserved. Velopack uses a three-part package
version: `Major.Minor.(Revision * 65536 + Build)`. For example, `0.5.0.71` becomes
`0.5.71`, and `0.5.1.0` becomes `0.5.65536`. This preserves ordering when either
Revision or Build increases. Verify the mapping with `./scripts/Test-PackageVersion.ps1`.

For managed installations, editable levels and assets live in
`%LocalAppData%/SamElla/Content/Levels` and `.../Assets`, outside the replaceable app
directory. New defaults are copied forward only if the old copy was not edited;
custom files and intentionally deleted defaults are preserved. Unmodified retired
defaults are removed. The existing `--levels <directory>` override still works.

## Hubmle Beginnings

NetMan started out as a side-scrolling DOS pixle art game. The orginal code can be found in the Legacy folder [NetMan-0.1](Legacy/NetMan-0.1/README.md) which preserves the original console version.

## Developer Notes

* Active development lives in [SamElla](SamElla/README.md)
* More [developer notes](Docs/dev-notes.md)
