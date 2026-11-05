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
