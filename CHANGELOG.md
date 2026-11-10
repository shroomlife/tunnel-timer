# Changelog

## [1.0.1.0] - 2026-11-10

Stability pass after an independent review. The app is meant to run unattended all day, so every path that could end the process or leave a countdown stuck was closed.

### Fixed
- **A locked or full disk could end the app or leave a widget that could not be dismissed.** Saving the meetings no longer decides whether a change applies: the list changes and the widget reacts first, a failed save is logged and repeated on the next pass.
- **A damaged meetings file could crash every start.** Loading never throws; an unreadable file is set aside. Empty entries are dropped and a stored link that is not a web address is removed, so a hand-edited file cannot start another program.
- **Errors in click and timer handlers ended the process without a trace.** They are logged to `app.log` in the app data folder and the app keeps running. One failed countdown pass no longer stops the countdown.
- **Start with Windows, opening a meeting link and the notification area icon** can each fail without taking the app down. Without an icon the panel opens by starting the app again.
- **Registering for crash restart** no longer prevents the start if Windows refuses it.
- **The panel was taller than the screen on small or strongly scaled displays.** It is shortened to the work area and its middle part scrolls.
- **Clicking the notification area icon while the panel was open wiped what had been typed.** The form is only reset when the panel opens.
- **A remembered widget position partly outside the screen** after a resolution or monitor change is ignored.
- **A long overdue time** (`+100:00` and more) is scaled to fit instead of being cut off.
- Meetings are flushed to the disk before the file is replaced, so a power loss cannot leave an empty file.

### Changed
- **A start time that has already passed today means tomorrow.** Late in the evening a meeting just after midnight can now be entered. The hint under the time says "tomorrow, in ...".
- A change of the Windows time zone while the app is running is picked up when a meeting is entered.

### Known limits
- The widget lives on the virtual desktop it appeared on. On another virtual desktop it is not visible.
- If opening a meeting link fails, the widget stays but gives no message.

## [1.0.0.0] - 2026-11-10

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
