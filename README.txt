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
  Home (profile card with photo, student ID, grade, school), Grades (click a class for its
  trend and assignments), Work (click an assignment for score, details and teacher comments),
  Schedule (period cards, Now/Next once you add bell times in Settings), Inbox (messages and
  announcements), Docs, Tools (what-if grade calculator, workload and score charts, CSV and
  calendar export), Settings (8 themes, own photo/logo, bell times, refresh interval).
  Photo and logo come from your school's server. To use your own, put photo.png / logo.png
  in %APPDATA%\CampusMidnight or use Settings.
  Inbox, announcements, documents and the photo address are best guesses; the app also reads
  your portal's own scripts to find the real addresses. Diagnostics shows what worked.
