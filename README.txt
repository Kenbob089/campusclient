CAMPUS MIDNIGHT (native Windows app)
====================================
Build (one time, nothing to install):
  Double-click build.bat
  -> dist\Campus-Midnight.exe  (one portable file, about 150 KB; copy it anywhere)

It is a normal Windows program written in C#: no Electron, no embedded browser.
It needs Windows 10/11 with .NET Framework 4.x (already included with Windows).

First run:
  1. Paste the link you normally use to sign in to Campus
     (e.g. https://yourdistrict.infinitecampus.org/campus/portal/students/yourdistrict.jsp)
  2. Type your Campus username and password and press Sign in.
  3. Tick "Stay signed in on this PC" to skip this next time (password is encrypted
     with Windows for your account only).

School uses Google/Microsoft sign-in? Click "My school uses Google or Microsoft sign-in",
sign in to Campus in your normal browser, then copy the "cookie" request header from
F12 > Network and paste it in.

Windows may show "Windows protected your PC" for an unsigned app: More info > Run anyway.

If a page says nothing was found: Settings > Diagnostics > Copy report, and send it back.
The report lists addresses, status codes and field names only, never your data.

NO WINDOWS PC TO BUILD ON? LET GITHUB BUILD IT
  1. On github.com create a new PRIVATE repository (any name).
  2. Upload everything in this folder, including the hidden .github folder
     (Add file > Upload files, then drag the contents in). If .github does not
     upload, use Add file > Create new file, type  .github/workflows/build.yml  as the
     name, and paste in the contents of that file from this zip.
  3. Open the Actions tab. The "Build Campus-Midnight.exe" run starts by itself
     (takes about a minute). When it is green, open the repo's Releases section
     (right side) and download Campus-Midnight.exe.
  If the run is red, open it and copy the error lines to me.

If the app closes or shows an error, details are saved to %APPDATA%\CampusMidnight\crash.txt
If Windows refuses to start the downloaded exe: right-click it > Properties > tick Unblock > OK.


WHAT'S IN THIS VERSION
  Home (profile card, a live "now" strip, GPA, work due), Now (Saturn-style: ring and countdown
  for the current class, what is next, and today's timeline), Grades (average, unweighted and
  weighted GPA, dropped classes shown separately), Work, Schedule (period cards, dropped classes
  dimmed with a Dropped badge, filter by A/B day), Inbox, Docs, Tools, Settings.

  Bell schedules: Settings > Edit bell schedules. Use "Fill in a whole day" (first class time,
  class length, passing time, lunch) or type each period. Name periods 1, 2, 3... to link them to
  your Campus periods. Make extra schedules (Early release...) and pick them per weekday or for today only.
  A/B days: turn on "My school rotates days", set the names (A,B), then press today's letter.
  The rotation skips weekends and days you mark as no school. Each class can be set to meet
  every day or on one day only.

  GPA: unweighted 4.0 scale from each class's letter. Weighted adds 0.5 for honors and 1.0 for
  AP / IB / dual credit. The level is guessed from the class name; open a class to change it.

  Light, dark or match Windows: the moon button at the top, or Settings > Appearance.
  The sidebar collapses with the button at its bottom.
  The app icon is built from your school logo when it is roughly square; otherwise the built-in
  moon icon is used. "Class of" is worked out from your enrollment's end year and grade.
  Photo and logo come from your school's server. To use your own, put photo.png / logo.png
  in %APPDATA%\CampusMidnight or use Settings.
  Inbox, announcements, documents and attendance addresses are best guesses; the app also reads
  your portal's own scripts to find the real addresses. Diagnostics shows what worked and what
  it tried.

NEWER ADDITIONS
  Bell times and A/B days are read from Campus when it provides them (the school calendar tells
  which day type each date is; the class list tells which period each class meets in). If the
  app finds your bell times it imports them on first run; otherwise use Settings > Edit bell
  schedules > Fill in a whole day. In the editor each period has a "class in this period" button:
  pick a class yourself, or leave it on Automatic. With A/B days you can pick per day.
  The top-right menu (three dots) holds Refresh, Original site, Light/Dark and Sign out.
  Settings: count dropped classes in GPA, show numbers instead of letters, class start/end
  notifications (Windows notification centre; keep the app in the tray to keep getting them),
  and a sidebar editor to reorder or hide buttons. 22 accent themes.
  "Open in Campus" opens your school's sign-in link (Campus has no deep links that work for every district).

GUIDED SETUP
  Runs by itself after your first sign-in (and from Settings > Bell schedules > Guided setup): look and
  feel, bell times (with mods and lunch), rotating days (whole day, or before / after lunch, and which
  periods rotate), which class is in which period, then extras. Logs: Settings > Diagnostics shows recent
  activity; the full file is %APPDATA%\CampusMidnight\log.txt.

ROUND 7 (setup redo)
  - Setup step 1 is a grid: letter days across, time rows down, circles for periods (M and Lunch as grey blocks).
    Click a circle to choose it, click a time to edit it, click a letter to rename/move/remove it.
    Rotation helper fills the other days (whole day or before/after lunch). Today's letter or Campus calendar letters.
  - Step 2 picks the class for each period number. Classes are only filled from Campus when you press "Auto fill from Campus".
  - Mods and the old bell editor page were removed.
  - Photo and app icon: crop, zoom, rotate and drag before saving (Settings > Pictures).

ROUND 8
  - Schedule tab: month calendar with the letter for each day, the classes for the picked day and a menu to mark it
    No school / Regular / a saved schedule (half day etc.). Saved schedules: menu > New saved schedule.
  - "Change times and days" opens only the schedule grid (Save, or Save and set classes).
  - Charts show details on hover. Menus restyled (also the tray menu). Photo can be hidden in Settings.

ROUND 9
  - Menus are now custom-drawn popups (rounded, fade in, keyboard Up/Down/Enter/Esc). The tray menu uses the same one.
  - Settings > Pictures is three cards (photo, app icon, logo). "Use Campus Midnight icon" resets the app icon.
  - Schedule calendar is smaller and sits beside the selected day on wide windows. Times show AM/PM.
  - New gear icon, new login page with the Campus Midnight icon. Day plans are cached for speed.

ROUND 10
  - Schedule setup: "Last day of school" date (month, day, year). After that day the schedule stops filling in and the saved schedule is cleared.
  - "Rotate the periods for me" toggle in the schedule setup (off = set each letter day by hand). The letter button is disabled on weekends and days off.


==================== WPF REWORK (preview) ====================
Folder: wpf\   Build: .github\workflows\build-wpf.yml  (dotnet publish, .NET 8, self-contained single exe)
Output: Campus-Midnight-WPF.exe. Nothing to install; the runtime is inside the exe (about 70 MB).
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

The WPF app is now called Noctua (the file is still Campus-Midnight-WPF.exe). Settings stay in %APPDATA%\CampusMidnight so both apps share them.
Export schedules: Schedule page or Tools > Bell schedules... lets you tick bell schedules and save them as text, CSV or JSON.

Notifications (WPF): the app checks for new assignments, new grades and new messages every minute (separately from the 5-minute refresh) and tells you
in the bell at the top right and in Windows. Turn it off in Settings > Notifications. If the internet drops, a red "Failed to load" pill appears
instead of signing you out; press it to reload and sign in again. In the demo school, every refresh adds a new pop quiz so you can see it work.
