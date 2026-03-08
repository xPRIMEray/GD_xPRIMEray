@echo off
setlocal

echo ============================================
echo Running xPRIMEray Render Test (EINSTEIN GRIN FAST COMPARE)
echo ============================================

set GODOT_EXE="C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
set PROJECT_PATH=C:\godot\godot_xPRIMEray
set LOG_FILE=logs\ab_einstein_grin_fast_compare.log
set BUILD_LOG_FILE=logs\ab_einstein_grin_fast_compare.build.log

cd /d %PROJECT_PATH%
if not exist logs mkdir logs

rem Rebuild from current C# source before running the harness.
set HOST_USERPROFILE=%USERPROFILE%
set DOTNET_CLI_HOME=%PROJECT_PATH%\.dotnet_cli
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_NOLOGO=1
set NUGET_PACKAGES=%HOST_USERPROFILE%\.nuget\packages
if not exist "%DOTNET_CLI_HOME%" mkdir "%DOTNET_CLI_HOME%"
if not exist "%NUGET_PACKAGES%\godot.net.sdk\4.5.1" (
  echo ERROR: Missing Godot.NET.Sdk cache at "%NUGET_PACKAGES%\godot.net.sdk\4.5.1"
  exit /b 1
)

dotnet build "Physical Light and Camera Units.csproj" -c Debug -v minimal > %BUILD_LOG_FILE% 2>&1
if errorlevel 1 (
  echo ERROR: dotnet build failed. See %BUILD_LOG_FILE%
  exit /b 1
)

rem Keep Godot user:// writable and local to this workspace.
set APPDATA=%PROJECT_PATH%\.appdata
set LOCALAPPDATA=%PROJECT_PATH%\.localappdata
set USERPROFILE=%PROJECT_PATH%\.userprofile
if not exist "%APPDATA%\Godot\app_userdata\GRIN Physical Ray Tracing\logs" mkdir "%APPDATA%\Godot\app_userdata\GRIN Physical Ray Tracing\logs"

%GODOT_EXE% --path . -- --render-test --render-test-fixture=einstein_ring_minimal --render-test-profile=einstein_compare_fast --einstein-transport-model=grin --lifecycle-stress=0 --smartscale=0 > %LOG_FILE% 2>&1

python tools\renderhealth_regress.py %LOG_FILE%

echo.
echo Finished EINSTEIN GRIN fast comparison test.
if /I not "%NO_PAUSE%"=="1" pause
endlocal
