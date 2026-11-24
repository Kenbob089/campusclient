NOCTUA (Windows app, formerly Campus Midnight)
==============================================
Source is in wpf\ (C#, .NET 8, WPF). Build: .github\workflows\build-wpf.yml on GitHub Actions
produces Noctua.exe: one portable file with the runtime inside, nothing to install.
The old WinForms app has been removed. Settings still live in %APPDATA%\CampusMidnight\settings.txt.

==================== WPF REWORK (preview) ====================
Folder: wpf\   Build: .github\workflows\build-wpf.yml  (dotnet publish, .NET 8, self-contained single exe)
Output: Noctua.exe. Nothing to install; the runtime is inside the exe (about 70 MB).
It reads and writes the same %APPDATA%\CampusMidnight\settings.txt as the WinForms app, so your
schedule, theme and sign-in carry over.

In this first WPF build: sign-in (password or cookie), Home, Now (live countdown), Grades with course
detail, Assignments, Schedule (calendar + day list + per-day schedule type), Settings (mode, 19 accent
themes, GPA/number options, pictures, refresh interval), Diagnostics.
Not ported yet (still in the WinForms app): setup wizard / grid editor, crop tool, Tools page (what-if,
charts, exports), Inbox, Docs, tray notifications, sidebar editor, saved-schedule editor.
This code has not been compiled by me (no compiler in my sandbox). Send me any build errors and I will fix them.

WPF update: opens maximized; class and assignment details open in their own windows (click a class tile,
a class in a day list, or any assignment); new Tools page (what-if calculator, due-next-14-days and
score charts with hover, score trend, CSV and calendar exports); sidebar labels hide when collapsed;
single-file exe no longer compressed (faster start).

WPF build (wpf/ folder, uncompiled until you build it on GitHub Actions):
  Ported pages: Home, Now, Grades, Work, Schedule (calendar, saved schedules, All classes),
  Inbox, Docs, Tools, Settings, Diagnostics, first-run Setup, class/assignment/message windows,
  tray + notifications, picture cropper. Settings are shared with the WinForms app.

Demo account (WPF build): on the sign-in screen use the link  example.com
  username: demo     password: midnight
It only works when the link is example.com (or a subdomain of it). It shows a pretend school and
clears its sample schedule when you sign out.
Sidebar: drag a button to move it, right-click it to hide it (Settings > Sidebar brings it back).

The WPF app is now called Noctua (the file is Noctua.exe). Settings stay in %APPDATA%\CampusMidnight so both apps share them.
Export schedules: Schedule page or Tools > Bell schedules... lets you tick bell schedules and save them as text, CSV or JSON.

Notifications (WPF): the app checks for new assignments, new grades and new messages every minute (separately from the 5-minute refresh) and tells you
in the bell at the top right and in Windows. Turn it off in Settings > Notifications. If the internet drops, a red "Failed to load" pill appears
instead of signing you out; press it to reload and sign in again. In the demo school, every refresh adds a new pop quiz so you can see it work.

Zoom: Ctrl + mouse wheel, or Ctrl + Plus / Minus (Ctrl + 0 resets). Version is 1.0.<number of updates> and shows at the bottom of Settings. Schedule import lives on the setup screen (Your schedule step).

Several schedules: in the setup (Your schedule step) add a schedule next to Regular, like Half day. Each has its own times and periods. A schedule can be used automatically when Campus names the day (Early Dismissal), every week on a weekday, or picked for any date on the Schedule page. The Home tile shows which one is used today.
No letter days: tick "My school does not use letter days" in the same step.
Import/Export: Export lets you tick schedules and letter days. Import (setup screen) shows what is in the file so you can tick what to bring in.
Open in Campus opens Campus in a window inside Noctua (needs the Microsoft WebView2 runtime, already on Windows 10/11 with Edge).

v1.0.18: Period names setting (Settings), Open in Campus goes to the matching Campus page with a Home button, class window shows the current quarter and has a GPA settings page, softer light-mode shadows, exe is now Noctua.exe.

v1.0.20: announcements read from /campus/resources/portal/userNotice; the Campus window records the portal addresses it sees (captured-<host>.txt) and the app tries them for messages/documents/attendance; graphs get a faded fill.

v1.0.22: checks every 30 seconds; message text is read from Campus messageView.xsl; the app opens attendance/documents/message pages out of sight (once per 12 h while those are empty) to learn their addresses.

v1.0.23
- Menu dividers are softer and follow the theme (darker in dark mode, lighter in light mode).
- Clicking a notification opens the exact assignment, class or message. The bell sits right of the quarter button.
- Opening the notification list clears the count on the bell; the rows stay marked as new until the next time you open it.
- Menus, the notification list, chart tips and the class and assignment windows follow the app zoom. Settings has an Accessibility section with a zoom reset.
- Midnight is now black with a white accent (Daylight in light mode). The Neon theme became Campus green.
- What-if calculator "graded points so far" only counts the quarter you are viewing.
- A dropped class no longer shows in quarters that begin after it was dropped, unless Campus has a grade or work for it there.

v1.0.24
- Campus theme uses #92c841 in light mode and a darker green in dark mode.
- Dividers and other lines are back to how they were. Text on accent colours is white again (only Midnight differs).
- Accessibility sits just above More in Settings.
- Dropped classes: empty grading tasks no longer count as data, and a dropped class with no end date is hidden in quarters where it has no grade or work.
- The old WinForms app and its build files were removed.

v1.0.25
- Tools has a switch at the top between Classic tools and New tools (final exam planner, GPA projector, countdown, focus timer, score averager, missing work).
- Dropped classes no longer show in any later quarter.
- Messages from another year show the year in the date.
- Message text is now read through the built-in web view when a plain download only returns an empty form.

v1.0.26
- Tools is one page again: the what-if calculator with the countdown beside it. The other new tools were removed.
- Settings has a switch to hide the system tray icon.
- Dropped classes: every quarter's entry is marked dropped, and a dropped class is hidden in quarters after the one it was dropped in.
- "B Early Dismissal" days use your B column (and your Half day schedule), not the A column.
- When today is not a school day, "today is" in the setup applies to the next school day. The ... button on the Schedule page can override the letter day for one date; Export schedules moved into it.
- New Adaptive theme: grades, statuses and tags keep their own meaning colours (green high, red low).
- Message text: Campus' own link is tried first; more id combinations are tried; diagnostics note the shape of the message link.

v1.0.27
- New: Custom theme (beta), schedule settings in exports/imports, in-app pop-up notifications (Settings > Notifications),
  nav-rail notification dot, initials avatar when photo hidden, demo2 (bad student) account, Ctrl+Z in schedule setup,
  single "..." import menu, last day of school auto-enabled from Campus, semester classes can share a period across terms.
- Fixes: toggle knob colour on Midnight, text box hint alignment, page no longer jumps when picking a menu option,
  setup errors shown at the top, letter-day tile hidden when letter days are off, dropped classes hidden after their end date,
  message link address, attendance now tries the enrollment event list.
- Still unresolved: Documents (address not found yet), message body text. Send diagnostics again.

v1.0.28
- Data folder is now %APPDATA%\Noctua (the old CampusMidnight folder is moved over on first start).
- Setup errors sit in a fixed strip above the page. Hint text and typed text line up in text boxes.
- Your real schedule is parked while a demo account is signed in and restored afterwards.
- Now card on the home page opens the Now page when a schedule exists (also on days off).
- Remembers which portal address worked for each module and stops asking dead ones; Debug page has a "Legacy addresses" toggle.
- Attendance card on the Tools page. Diagnostics list the addresses the portal's own pages asked for (to find Documents).
- Custom theme colour picker (click a swatch).
- Import with "Replace the one I have" no longer loses the grid when schedule settings are imported too.
- "Rotating the periods" setting is part of schedule settings exports and imports.

v1.0.29
- New Attendance page (per-class totals, click a class for a detail window); removed from Tools.
- Custom theme only appears after tapping the version number 8 times (again to hide it).
- New Ocean gradient theme. In-app notifications turn the tray option off. Countdown "+" for your own countdowns.
- What-if calculator starts from the grade Campus shows and its boxes have hints.
- Hint text is aligned to the real typing position; theme preview bar no longer cut off.
- Documents: also tries /portal/report/all.

v1.0.30
- Documents show a file-type icon and name the type; they open in the Campus web window.
- Attendance has its own icon. Demo accounts have attendance data (demo2 has plenty) and more demo documents.
- New countdowns can use school days only.

v1.0.31
- Attendance: one-letter statuses (U, E, X) become words and land in the right row; table corners fixed; Settings toggle "days absent".
- Now page full screen mode (F11): big time-left card, menu (bottom left), info widget (bottom right), schedule below (arrow), first-use welcome tour, exit hint.

v1.0.31.1 (bug fixes)
- Full screen tour moves to each piece (menu, info, arrow) with no dark background after the first card.
- Full screen schedule is large and fills the screen, no box.
- "Show days absent instead of number of total absences" has its own Attendance section in Settings.

v1.0.31.2 (bug fix)
- Full screen tour: the Next / Skip / Done buttons work after "Show me around".

v1.0.31.3 (bug fix)
- Days absent counts only days when every class scheduled that day was missed.
- Full screen schedule: class titles slightly smaller, a bit more space between rows.

v1.0.31.4 (bug fix)
- Full screen menu has a Settings button: popup with General (Show clock, Clock location) and Themes pages.

v1.0.31.5 (bug fix)
- Full screen settings: pages listed on the left, settings on the right; clock location is a drop-down; Themes page is now Appearance (with light/dark mode).
- Drop-down menu dividers follow the theme. "Open original Campus site" is now "Open Campus".

v1.0.31.6 (bug fix)
- Full screen settings panel keeps one size; theme previews and the full screen colours refresh when mode or accent changes; clock is smaller.

v1.0.32
- Full screen: notifications appear top right (under the clock when it is there); test notification button when special features are on.
- Tour tips sit higher, Skip has more room and is hidden on the last tip. Clock tucks above the up arrow on the schedule screen.

v1.0.33
- Full screen: optional notification button (top right, off by default; setting in full screen General page). The clock stays to its left. Click it for a list of recent notifications.
- Tour tips for Menu moved down a little and Info moved up.

v1.0.33.1 (bug fix)
- Full screen: scroll wheel / two-finger swipe moves between the time-left page and the schedule; the notification button is now the app's normal bell and panel.
- Demo accounts: realistic assignment names, a new assignment/grade only every so often (not every refresh), more messages, and whole-day absences so "days absent" shows something.

v1.0.33.2 (bug fix)
- Demo: a new item every 5 refreshes. Opening the notification menu clears pop-up notifications. Full screen clock and bell share one row (same height, clock does not move when the bell is on).

Installer (separate project, installer/)
- NoctuaSetup.exe installs Noctua for the current user only (no admin prompt) into %LOCALAPPDATA%\Programs\Noctua.
- It downloads Noctua.exe from the wpf-latest release of Kenbob089/campusclient (or uses a Noctua.exe sitting beside it, for offline installs).
- Adds a Start menu shortcut, optional desktop shortcut, and an entry in Settings > Apps (HKCU uninstall key). Re-running it updates in place.
- It keeps a copy of itself as Uninstall.exe (/uninstall). Uninstall can also delete %APPDATA%\Noctua.
- Built by .github/workflows/build-installer.yml into the "installer-latest" release. The installer has its own version (starts at 1.0.0).

v1.0.34 (Noctua, feature update) + installer 2.0.0
- Settings has an "Updates" section near the bottom: current version, installed/portable, last checked, "Check now", "Update now",
  and "Update automatically when I'm not using Noctua". Noctua checks GitHub every 10 minutes and sends a notification when a new version exists
  (it says it will update once you're inactive when auto-update is on). The update swaps the exe, opens Noctua again and shows "Update complete".
  Inactive = no keyboard/mouse for 3 minutes while the window is in use, or 1 minute if it is minimized or in the tray. Never during full screen Now mode.
- The build workflow now also publishes version.txt (read from the csproj) to the wpf-latest release. The installer and the updater read it.
- Data folder: a copy installed by the installer keeps data in Noctua\Data next to Noctua\App (settings are copied from %APPDATA%\Noctua once).
  A portable Noctua.exe still uses %APPDATA%\Noctua.
- Installer: custom title bar in the app's colours, screens (welcome with GitHub/installed versions, source, options, progress, done), install from a local file,
  closes a running Noctua and says so, start with Windows option, reinstall/uninstall from the welcome screen, new layout uninstaller.

v1.0.35 + installer 2.1.0
- Installer is now a .NET Framework 4.8 exe (about 200 KB, opens instantly; .NET Framework 4.8 is part of Windows 10 and 11). Matches Windows light/dark mode, stacked version cards, banner image, What's new screen, crash details saved to %TEMP%\NoctuaSetup-error.txt.
- Noctua: launching it again while it is running (or in the tray) just opens the window. Tray menu has "Open Now view" (full screen Now).
- Noctua: What's new window, bug fix number shown in versions, documents open in separate windows, full screen gets an "update ready" notification instead of restarting.
- The app workflow publishes version.txt and changelog.txt (from wpf/CHANGELOG.txt) on the wpf-latest release. Keep CHANGELOG.txt updated each release.

v1.0.35.1 (bug fix) + installer 2.1.1
- Installer: the GitHub version and the change list now come back on the window's own thread (fixes the "different thread owns it" crash and the empty GitHub card).
- Noctua: update results are always handed to the UI thread.
- Zips are now separate: Noctua.zip (wpf/, build-wpf.yml, README) and NoctuaInstaller.zip (installer/, build-installer.yml). Both unpack to the repo layout.
