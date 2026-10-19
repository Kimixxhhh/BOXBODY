@echo off
setlocal
cd /d "%~dp0"
dotnet build "tools\WeaponWorkbench\WeaponWorkbench.csproj" --nologo -c Release
if errorlevel 1 (
  echo Weapon editor build failed.
  pause
  exit /b 1
)
start "Weapon Workbench" "%~dp0tools\WeaponWorkbench\bin\Release\net10.0-windows\WeaponWorkbench.exe" --project "%~dp0."
