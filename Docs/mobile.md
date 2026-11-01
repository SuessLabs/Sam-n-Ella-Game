# Mobile and desktop hosts

The Windows desktop executable and .NET MAUI mobile app share `SamElla.Core`:
gameplay, levels, sprites, the software renderer, synthesized sound samples,
settings, records, leaderboard, and the frame-driven `GameSession`.
The original source remains under `SamElla/`; the core project compiles the
portable files, and the desktop project compiles only its Windows host and updater.

Mobile Settings includes **Large Controls (1.5x)**. It enlarges all eight touch
targets immediately, saves the choice across launches, and defaults to off.
Start/Select stay near the bottom middle, with spacing adjusted to prevent
overlap on narrow screens. Changing the size requires held fingers to lift before
they can activate the resized controls.

On the mobile title screen, enter **Up, Up, Down, Down, Left, Right, Left, Right,
Attack, Jump** to activate the Konami Code (Attack = B, Jump = A). The final Jump
only activates the code and leaves the title open. Release it, then press Jump
again to start with the existing 30-life power-up bonus. Normal Jump menu
confirmation and Attack cancellation on other screens continue to work.
Successful entry plays an original three-second chiptune sting with syncopated
pulse leads, crunchy triangle bass, and noise percussion. It uses the Sound FX
volume and pauses background music until the cue finishes on both hosts.

While game content and audio load, the mobile page displays
`Docs/GooglePlay/images/feature-graphic-1024x500.png`, stretched to fill its
available screen area. It is bundled as `feature_graphic.png`, so loading artwork
works offline. The platform launch splash still uses the app badge.

`SamElla.slnx` remains the desktop/test solution and needs no mobile workloads.
`SamElla.Mobile.slnx` contains the mobile host and core.

## Windows

Use the existing `build.bat` / `build.ps1` commands, including `-Offline`.
The Win32 window, keyboard controls, native audio, Native AOT packaging,
Velopack installer, and stable/prerelease update preferences remain on Windows.

## Android

Install .NET 10, the `maui-android` workload, Android SDK platform 36 and build tools,
and a compatible JDK (the installed Android OpenJDK works on this Windows machine).

```powershell
dotnet workload install maui-android
./build-mobile.ps1
```

The default produces a development-signed ARM64 APK under
`Publish/Mobile/android-arm64/`. Install its `*-Signed.apk` on a phone, or use:

```powershell
adb install -r Publish/Mobile/android-arm64/com.suesslabs.samella-Signed.apk
```

For an x64 Android emulator, pass `-Runtime android-x64`. A JDK override is
available through `-JavaSdkDirectory`. For cached builds, restore the same target
and runtime once, then use `-Offline`:

```powershell
dotnet restore SamElla.Mobile/SamElla.Mobile.csproj -p:MobileTargetFramework=net10.0-android -r android-arm64
./build-mobile.ps1 -Offline
```

Windows and MAUI read `FileVersion`, `AssemblyVersion`, and `MobileBuildNumber`
from the root `GameVersion.props` during project evaluation, including IDE builds.
Android displays the full Windows version (for example `0.8.0.123`) and uses
`MobileBuildNumber` for its numeric version code. iOS uses the three-part display
version (`0.8.0`) and the same mobile build number (`123`); its assembly and in-game
version still contain all four parts. The mobile counter keeps increasing when
the release prefix changes and its fourth component is reset.

`-BuildNumber` explicitly overrides the Android version code when needed; ordinary
builds should use the shared value. Use a persistent private signing key for
distributed updates. `-Preview` uses `com.suesslabs.samella.preview` and writes to
`Publish/Mobile/Preview/<runtime>/`, keeping CI installs separate from releases.
No signing credentials or store publication are configured by this port.

Every opened, updated (`synchronize`), or reopened PR runs the Windows/mobile
workflow, including PRs whose pushes only change documentation. In its disposable
checkout, `.github/scripts/Set-CiBuildVersion.ps1` adds the workflow run number to
the checked-in fourth component and mobile counter. Windows and Android build
from that same file. Rerunning the same workflow run checks out fresh source and
reuses the version; a later PR run receives a higher increment. No version commit
is pushed to the PR branch. Preview numbers are CI-only and must not be uploaded
to the production Play listing. Develop/master push builds use the committed
version; the existing develop merge automation increments the shared file once.

Check version evaluation and CI increments locally:

```powershell
./scripts/Test-BuildVersions.ps1 -IncludeIos
```

Omit `-IncludeIos` on machines with only the Android workload installed.

## iPhone and iPad

The MAUI project also targets `net10.0-ios`, with an iOS application delegate,
landscape phone/tablet configuration, graphics, touch controls, and audio.
Compile checks can run with the installed iOS reference packs on Windows;
building, signing, and deploying the actual iOS app requires a Mac with compatible
Xcode, the `maui-ios` workload, and Apple signing/provisioning.

On that Mac, select the iOS target in the IDE, or build the simulator target:

```sh
dotnet build SamElla.Mobile/SamElla.Mobile.csproj -p:MobileTargetFramework=net10.0-ios -r iossimulator-arm64
```

Use `iossimulator-x64` on an Intel Mac. Device/archive builds use `ios-arm64`
with the appropriate signing identity and provisioning profile.

## Controls, display, and lifecycle

The four-arrow D-pad always appears as a cross: Up above Left/Right, and Down
below. Up retains the Windows Up-arrow jump mapping during gameplay. Start and
Select sit at the bottom center. Start toggles pause during gameplay and in the
pause menu; it does not confirm ordinary menu choices. Select cycles menu choices and is
inactive during gameplay. Jump accepts screen selections and Attack cancels or
goes back; in gameplay they retain their normal jump and weapon actions.
Attack and Jump sit side by side at the bottom right, with Roman numeral I above
Attack and an outlined circle above Jump. Inactive controls are dimmed.
Cancelling the mobile high-score entry skips submitting that score.

Fingers are tracked independently, so movement, jumping, and attacks work
together; dragging off a control, cancelling a touch, or suspending clears held
input. Menus and high-score initials use the D-pad and Jump to accept. Held
touches must be released after switching between gameplay and menus,
so holding Start cannot repeatedly open and close Pause. Start never sends an
ordinary menu-confirmation action; only Jump accepts those selections. Settings
can be closed with Attack or by selecting its Back row and pressing Jump; the Android Back button
also closes Settings or resumes from Pause.

On the initials screen, Jump moves to the next initial. On the third initial,
Jump submits the entry. Start is enabled on this screen and submits initials
immediately from any slot. The D-pad still cycles characters and moves between
slots, and Attack cancels without saving. Submitting leaves the score table
visible until another Jump press.

After death, retry stays locked until the animation and actual death-sound
playback both finish. A waiting message is shown during that time. Then only a
fresh Jump or Start press can begin again; arrows, Select, Attack, and touches
held during the animation cannot skip the wait. Start is enabled for retry once
the wait ends, and the final-life leaderboard also waits for the death sound.
Cancelling that leaderboard returns to the retry screen instead of starting a
new game through the Attack button.

The game stays at 384 x 240. A safe-area-aware landscape canvas scales it to the
largest size that fits the entire available display, preserving its 16:10 aspect
ratio without cropping. Fractional scaling avoids wasting space on phones whose
resolution is not a whole multiple of the game resolution; nearest-neighbor
sampling retains the pixel-art look. Wider devices receive side margins rather
than extra visibility into the level. Translucent touch controls overlay the
lower edges of the view instead of reserving a separate strip below the game.
Control placement adapts to phone/tablet size and density.

Leaving the app pauses gameplay, saves preferences/records, clears touches, and
silences audio. Returning leaves gameplay paused until the player resumes, with
no countdown catch-up. Settings and records live in the mobile app's private data
directory. Assets and levels are bundled for offline play. Android/iOS do not
include Velopack or its experimental-update switch; use platform distribution for
mobile updates.

SkiaSharp presents the pixel buffer; Plugin.Maui.Audio plays the same generated
PCM sounds/music as Windows, with the existing volume defaults and music/effect
switches. Native playback is prepared on an audio worker rather than the game
thread. No microphone permission is requested.

## Validation

```powershell
dotnet test --project SamElla.Tests/SamElla.Tests.csproj --configuration Release
```

Tests cover the shared session, simultaneous touches and releases, phone/tablet
viewport geometry, mobile Settings, and suspend/resume, alongside the game tests.
The mobile CI workflow builds Android and checks every manifest entry against
the packaged APK. Device testing should cover three-finger movement/jump/attack,
slide-off/cancel, Settings, initials entry, both orientations, audio volume,
Home/lock/resume, and a full level completion. APK compilation and package
inspection do not replace that device check.
