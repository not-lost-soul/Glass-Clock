@echo off
setlocal enabledelayedexpansion
title Building GlassClock
cd /d "%~dp0"

rem --- find the C# compiler that ships with Windows (no installs needed) ---
set FW=%windir%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%FW%\csc.exe" set FW=%windir%\Microsoft.NET\Framework\v4.0.30319
if not exist "%FW%\csc.exe" (
  echo Could not find the .NET Framework 4 compiler in "%windir%\Microsoft.NET".
  pause & exit /b 1
)

rem --- embed the fonts so GlassClock.exe is self-contained ---
set RES=
for %%f in (fonts\*.ttf fonts\*.otf) do set RES=!RES! /resource:"%%f","%%~nxf"

if not exist dist mkdir dist
taskkill /f /im GlassClock.exe >nul 2>&1

echo Compiling...
"%FW%\csc.exe" /nologo /target:winexe /optimize+ /nowarn:618,168,219,414 /out:dist\GlassClock.exe /lib:"%FW%\WPF" ^
  /r:PresentationCore.dll /r:PresentationFramework.dll /r:WindowsBase.dll /r:System.Xaml.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  !RES! src\*.cs src\Core\*.cs src\Glass\*.cs src\UI\*.cs
if errorlevel 1 (
  echo.
  echo ===== BUILD FAILED =====   Copy the error lines above into a GitHub issue.
  pause & exit /b 1
)

echo.
echo Built dist\GlassClock.exe  - starting it. Look for the tray icon (bottom-right) to open Settings.
start "" "%~dp0dist\GlassClock.exe"
timeout /t 3 >nul
