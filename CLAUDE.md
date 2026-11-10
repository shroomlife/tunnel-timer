# CLAUDE.md

## What this is

Tunnel Timer is a native Windows 11 app that keeps the start of the next meeting in view. The user enters a meeting through the notification area icon. Ten minutes before the start an always-on-top countdown widget appears. It can be dragged anywhere but not closed; it only goes away when the user presses "I'm in". After the start it counts up.

Built with WinUI 3 (Windows App SDK 2.6) and .NET 10 in C#, packaged as MSIX, prepared for the Microsoft Store.

## Build, test, run

```powershell
# Unit tests (Microsoft Testing Platform, selected in global.json)
dotnet test --project tests/TunnelTimer.Core.Tests

# Debug build, unsigned
dotnet build src/TunnelTimer.App/TunnelTimer.App.csproj -p:Platform=x64 -p:AppxPackageSigningEnabled=false

# Run the debug build: register the loose-file layout once, then start it
Add-AppxPackage -Register src\TunnelTimer.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AppxManifest.xml
Start-Process "shell:AppsFolder\shroomlife.TunnelTimerMeetingCountdown_szn2xxnwmd44j!App"

# Release MSIX, signed with the developer certificate, output in dist/
dotnet build src/TunnelTimer.App/TunnelTimer.App.csproj -c Release -p:Platform=x64

# Store package (unsigned, the Store signs it)
dotnet build src/TunnelTimer.App/TunnelTimer.App.csproj -c Release -p:Platform=x64 -p:UapAppxPackageBuildMode=StoreUpload

# Regenerate all icons and logos after changing the drawing
pwsh scripts/generate-icons.ps1
```

The quality gates are the compiler (zero warnings) and the unit tests. There is no linter.

## Architecture

```
src/TunnelTimer.Core/        net10.0, no UI dependency, fully unit tested
  CountdownCalculator.cs     meetings + current time -> CountdownSnapshot (phase, time until start)
  CountdownFormatter.cs      mm:ss before the start, +mm:ss after it
  MeetingSchedule.cs         open meetings, ordered, persisted on every change
  IMeetingStore.cs           persistence boundary
src/TunnelTimer.App/         WinUI 3, MSIX
  Program.cs                 custom entry point: single instance, crash restart registration
  App.xaml.cs                composition root: builds every object once, no static singletons
  Services/                  JSON store, ticker, tray icon, startup task, widget position, strings
  ViewModels/                CommunityToolkit.Mvvm, [ObservableProperty] on partial properties
  Views/WidgetWindow         the countdown widget
  Views/SchedulerWindow      the panel behind the notification area icon
  Strings/en-US, de-DE       every visible text, referenced through x:Uid or Strings.Get
tests/TunnelTimer.Core.Tests xunit.v3
```

All time logic lives in `Core`. The app project only contains windows, the tray icon and storage.

## Things that are easy to get wrong

- **The countdown is never counted down.** `CountdownTicker` recomputes the state from `TimeProvider` five times a second. That keeps it correct after standby and free of drift. Do not introduce a decrementing counter.
- **Widget window setup order matters.** `AppWindow.SetPresenter` comes first, the presenter properties after it. `IsAlwaysOnTop` set before `SetPresenter` did not reach the window.
- **Drag region.** Everything above the button is a caption region set through `InputNonClientPointerSource`. A passthrough region inside a caption region still hit-tests as caption, so the button row is simply left out of the caption rectangle.
- **The widget is shown without activation** (`AppWindow.Show(false)`), so it never takes focus from the user's work.
- **The widget cannot be closed.** `AppWindow.Closing` is cancelled, the window is hidden from Alt+Tab and the taskbar, and it has no title bar. The only way out is the "I'm in" button, which removes the meeting.
- **Meeting links open through `Launcher.LaunchUriAsync` as plain https.** Zoom's own `zoommtg://` scheme is registered by the Zoom client but Zoom withdrew its documentation and support, so the app does not build on it. The browser hands a Zoom link to the installed client. `MeetingLink.TryParse` only accepts http and https, so a stored link can never start another protocol handler.
- **Startup task manifest entry has no `Executable` or `EntryPoint`.** Both break the startup activation in Windows App SDK apps. `Enabled="false"`: the user turns it on in the panel.
- **Tray icon is created with `ForceCreate(enablesEfficiencyMode: false)`.** The default would pin the process to idle priority.
- **Assets are copied to the output directory** in addition to being package content, so the loose-file debug layout and the MSIX both find `Assets\AppIcon.ico` under `AppContext.BaseDirectory`.
- **H.NotifyIcon.WinUI is a prerelease on purpose.** The stable 2.4.x line depends on the Windows App SDK 1.6 metapackage and collides with the 2.x component packages.
- **Component packages, not the `Microsoft.WindowsAppSDK` metapackage**, which would ship Windows ML and Windows AI binaries.

## Known framework bug

A cross-process UI Automation client can terminate any visible WinUI 3 window (microsoft/microsoft-ui-xaml#11139, open, reproduces with Windows App SDK 2.6.5). No app code is involved, so it cannot be caught. Because the widget is visible by design, `Program.Main` calls `RegisterApplicationRestart`: Windows restarts the app after a crash once it has run for 60 seconds, the open meetings are loaded from disk, and the widget is back within seconds. A crash in the first 60 seconds is not restarted. Do not inspect the running app with `System.Windows.Automation`; that reproduces the crash.

## Brand

- Logo: rainbow tunnel on an ink tile. `docs/logo.svg` is the vector, `scripts/generate-icons.ps1` draws the same mark for every raster size (three arches below 48 px, six above).
- Colors and styles live in `App.xaml`: ink `#1B1A2E` is the app accent (overrides `SystemAccentColor*`), paper `#FBF8F3` the panel background, `RainbowBrush` the spectrum, one gradient brush per countdown phase, `PillButtonStyle` and `PillAccentButtonStyle` for actions. High contrast has its own dictionary.
- Outfit (bundled, `Assets/Fonts`, SIL Open Font License) for the name and digits, the system font for everything else.
- The rainbow appears once per surface: the grip on the widget, the logo in the panel.

## Conventions

- Code, identifiers and comments are English. Visible text is localized (English default, German).
- UI starts in light theme.
- Version is `Major.Minor.Patch.0` in `Package.appxmanifest`. Bump it with every packaged change and add a `CHANGELOG.md` entry.
- Package identity `shroomlife.TunnelTimerMeetingCountdown` is assigned by the Microsoft Store (Store ID `9P3TCQN74F6F`, reserved name "Tunnel Timer: Meeting Countdown"). `Properties/DisplayName` must equal the reserved name; the Start menu shows the shorter `VisualElements` name "Tunnel Timer". The first version section must not be 0, the last must stay 0.
- Data lives in the package's `LocalState` folder (`meetings.json`) and `LocalSettings` (widget position). Nothing is written elsewhere and nothing leaves the machine.
