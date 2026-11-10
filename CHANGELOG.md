# Changelog

## [0.1.0.0] - 2026-11-10

First version.

### Added
- Countdown widget that appears ten minutes before a meeting, stays on top of every window, can be dragged anywhere and cannot be closed. It counts up once the meeting has started and disappears only through "I'm in".
- Three looks for the widget: upcoming, last two minutes, overdue.
- Notification area icon that opens a panel to add a meeting by start time or in 15, 30 or 60 minutes, to list and remove open meetings, to start the app with Windows and to quit.
- Optional meeting link. With a link the widget offers "Join meeting", which opens the link in the default browser and dismisses the widget, plus "I'm already in". Only http and https addresses are accepted.
- Open meetings and the widget position survive a restart.
- Windows restarts the app after a crash.
- English and German.

### Known issues
- A UI Automation client can terminate visible WinUI 3 windows (microsoft/microsoft-ui-xaml#11139). The app is restarted by Windows when that happens, unless it had been running for less than 60 seconds.
