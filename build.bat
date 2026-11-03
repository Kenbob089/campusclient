@echo off
setlocal
cd /d "%~dp0"

rem Uses the C# compiler that is already part of Windows (.NET Framework 4). Nothing to install.
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find the built-in C# compiler at %CSC%
  echo You need Windows 10 or 11 with .NET Framework 4.x ^(it is included by default^).
  pause
  exit /b 1
)

if not exist dist mkdir dist
echo Building Campus-Midnight.exe ...
"%CSC%" /nologo /target:winexe /optimize+ /debug- /warn:1 /out:dist\Campus-Midnight.exe /win32manifest:app.manifest /win32icon:app.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Security.dll ^
  src\*.cs
if errorlevel 1 (
  echo.
  echo Build failed. Copy the red/error lines above and send them to me.
  pause
  exit /b 1
)
echo.
echo Done. Your app is: %~dp0dist\Campus-Midnight.exe
echo It is a single portable file: copy it anywhere and double-click.
pause
