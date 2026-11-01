# Mobile application icon

The MAUI launcher icon and splash screen reuse `Docs/Sam-and-Ella.png` directly.
This is the same 1254 x 1254 transparent PNG used for the Windows application
icon in `SamElla/Assets/AppIcon/app-icon.png`. There is no separate mobile artwork
copy to maintain.

`appicon.svg` supplies an opaque navy background. The PNG supplies the foreground
badge with Sam, Ella, the gold border, and brick ledge. MAUI generates all required
platform sizes. Android uses `ForegroundScale=0.65` to keep the badge inside the
adaptive icon's safe area for circular, rounded-square, and other launcher masks.
iOS uses the composed icon on an opaque background.

The Android manifest references `appicon` and `appicon_round`; the iOS asset
catalog is `Assets.xcassets/appicon.appiconset`. The splash screen resizes the same
badge to a 128 x 128 base size on the navy background.

After changing artwork, rebuild and redeploy the mobile app. If the launcher
still shows its cached old icon, clean and rebuild the mobile project before
deploying again.
