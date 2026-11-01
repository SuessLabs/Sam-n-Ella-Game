Actual Android captures - review before final submission
Captured 2026-11-01 from the connected Samsung SM-F966U1 foldable.
Installed app: com.suesslabs.samella, versionName 1.0.0, versionCode 1, targetSdk 36.
These document the currently installed build, not a verified final signed release.

01-character-select.png: Sam and Ella character-selection screen.
02-meadow-gameplay.png: Meadow stage, enemy, coins, pipe, and touch controls.
03-pipe-jump.png: Sam jumping beside a pipe with touch controls visible.

Exports are 2184 x 1758, 24-bit RGB PNG without alpha. Original captures were
2184 x 1968. Crop rectangle x=0, y=84, width=2184, height=1758 removes the phone's
status bar and bottom system taskbar. No game pixels were generated, composited,
stretched, or retouched. The game's own black letterboxing remains. The Samsung
Edge panel handle is still visible on the right; recapture with that overlay
hidden for the final gallery. Original device captures containing system UI are
kept outside the repository in the task scratch directory.

These foldable captures meet basic image size/aspect/format requirements, but
are not the 16:9 gameplay set recommended for promotional eligibility. For a
stronger launch gallery, capture three different gameplay situations at native
1920 x 1080 or higher on a 16:9 Android emulator/device. Include an outdoor stage,
the Underworld, and Speed Runs, ideally showing both playable characters.
Do not turn these into 16:9 by stretching them or hiding game UI. Retake them
against the final release if content, controls, or layout changes.

PowerShell 7 capture helper (Windows):
  .\scripts\Capture-GooglePlayScreenshot.ps1 -Name outdoor-gameplay

The helper captures the current screen; it does not launch the app or change
device settings. Inspect the game first. If more than one device is attached,
pass -Serial with the value shown by adb devices. Foldables may need -DisplayId:
  adb shell dumpsys SurfaceFlinger --display-id

After inspecting actual display bounds, crop only outside the app, for example:
  .\scripts\Capture-GooglePlayScreenshot.ps1 -Name foldable-gameplay -DisplayId 4630946449689556883 -Crop 0,84,2184,1758

Display IDs and crop bounds above belong to this capture session; do not assume
they apply to another device, rotation, navigation mode, or fold state. Preview
every capture for system overlays and private information before upload.

Suggested alt text:
01: Choose Sam or Ella from the Android game's character-selection screen.
02: Sam faces an enemy in the Meadow, with coins, a green pipe, and touch controls.
03: Sam jumps beside a green pipe in the Meadow using the Android touch controls.

https://support.google.com/googleplay/android-developer/answer/9866151
