@echo off
setlocal
cd /d "%~dp0"
set "GODOT_EXE=D:\godotCs\Godot_v4.7.1-stable_mono_win64.exe"
if defined GODOT_DOTNET set "GODOT_EXE=%GODOT_DOTNET%"
if not exist "%GODOT_EXE%" (
  echo Godot .NET was not found. Set GODOT_DOTNET to your Godot 4.7 .NET executable.
  pause
  exit /b 1
)
dotnet build FieldKit.csproj --nologo
if errorlevel 1 (
  pause
  exit /b 1
)
"%GODOT_EXE%" --headless --path "%~dp0." --editor --import
if errorlevel 1 (
  pause
  exit /b 1
)
start "Field Kit" "%GODOT_EXE%" --path "%~dp0."
