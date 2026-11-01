# Mobile and desktop hosts

The Windows desktop executable and .NET MAUI mobile app share `SamElla.Core`:
gameplay, levels, sprites, the software renderer, synthesized sound samples,
settings, records, leaderboard, and the frame-driven `GameSession`.
The original source remains under `SamElla/`; the core project compiles the
portable files, and the desktop project compiles only its Windows host and updater.

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

`-BuildNumber` is the Android version code (default 1 for local testing). Use an
increasing value and a persistent private signing key for distributed updates.
The script reads the display/file version from the Windows project's FileVersion.
No signing credentials or store publication are configured by this port.

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
pause menu; it does not confirm menu choices. Select cycles menu choices and is
inactive during gameplay. Jump accepts screen selections and Attack cancels or
goes back; in gameplay they retain their normal jump and weapon actions.
Attack and Jump sit side by side at the bottom right, with Roman numeral I above
Attack and an outlined circle above Jump. Inactive controls are dimmed.
Cancelling the mobile high-score entry skips submitting that score.

Fingers are tracked independently, so movement, jumping, and attacks work
together; dragging off a control, cancelling a touch, or suspending clears held
input. Menus and high-score initials use the D-pad and Jump to accept. Settings
can be closed with Attack or by selecting its Back row and pressing Jump; the Android Back button
also closes Settings or resumes from Pause.

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
