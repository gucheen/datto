@echo off
cd /d "%~dp0"
if not exist "src\Datto\bin\Debug\net10.0-windows\Datto.exe" (
  echo Datto has not been built. Run: dotnet build src/Datto/Datto.csproj
  pause
  exit /b 1
)
"src\Datto\bin\Debug\net10.0-windows\Datto.exe"
if errorlevel 1 (
  echo Datto could not start. See %%TEMP%%\Datto-startup-error.log for details.
  pause
)
