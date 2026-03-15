@echo off
setlocal EnableExtensions DisableDelayedExpansion

echo ============================================================
echo Running xPRIMEray Render Test Pair (STRAIGHT vs CURVED_MINIMAL)
echo ============================================================

set GODOT_EXE="C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
set PROJECT_PATH=C:\godot\godot_xPRIMEray

cd /d %PROJECT_PATH%

if not exist logs mkdir logs

set "TIMESTAMP=%DATE%_%TIME%"
set "TIMESTAMP=%TIMESTAMP:/=-%"
set "TIMESTAMP=%TIMESTAMP:\=-%"
set "TIMESTAMP=%TIMESTAMP::=-%"
set "TIMESTAMP=%TIMESTAMP:.=-%"
set "TIMESTAMP=%TIMESTAMP:,=-%"
set "TIMESTAMP=%TIMESTAMP: =0%"

set "STRAIGHT_LOG=logs\render_test_straight_%TIMESTAMP%.txt"
set "CURVED_LOG=logs\render_test_curved_minimal_%TIMESTAMP%.txt"
set "STRAIGHT_REGRESS_OUT=logs\renderhealth_regress_straight_%TIMESTAMP%.txt"
set "CURVED_REGRESS_OUT=logs\renderhealth_regress_curved_minimal_%TIMESTAMP%.txt"

call :run_fixture straight "%STRAIGHT_LOG%" "%STRAIGHT_REGRESS_OUT%"
if errorlevel 1 goto :fail

call :run_fixture curved_minimal "%CURVED_LOG%" "%CURVED_REGRESS_OUT%"
if errorlevel 1 goto :fail

echo.
echo ============================================================
echo Final Summary
echo ============================================================
echo Straight log: %STRAIGHT_LOG%
echo Curved minimal log: %CURVED_LOG%
echo.
echo Note: tools\renderhealth_summary.csv and tools\renderhealth_summary.json may be overwritten by the second parser run.
echo.

echo ============================================================
echo AutoCal Shadow Eval Summary (straight)
echo Source: %STRAIGHT_REGRESS_OUT%
echo ============================================================
powershell -NoProfile -Command "$found=$false; $inBlock=$false; Get-Content -LiteralPath '%STRAIGHT_REGRESS_OUT%' | ForEach-Object { if ((-not $inBlock) -and $_ -like '=== AutoCal Shadow Eval Summary*') { $inBlock=$true; $found=$true }; if ($inBlock) { $_ }; if ($inBlock -and $_ -eq '') { break } }; if (-not $found) { Write-Host '(summary block not found in parser output)' }"
echo.

echo ============================================================
echo AutoCal Shadow Eval Summary (curved_minimal)
echo Source: %CURVED_REGRESS_OUT%
echo ============================================================
powershell -NoProfile -Command "$found=$false; $inBlock=$false; Get-Content -LiteralPath '%CURVED_REGRESS_OUT%' | ForEach-Object { if ((-not $inBlock) -and $_ -like '=== AutoCal Shadow Eval Summary*') { $inBlock=$true; $found=$true }; if ($inBlock) { $_ }; if ($inBlock -and $_ -eq '') { break } }; if (-not $found) { Write-Host '(summary block not found in parser output)' }"
echo.

echo.
echo Finished paired render tests.
endlocal
goto :eof

:fail
echo.
echo FAILED paired render tests.
endlocal
exit /b 1

:run_fixture
setlocal
set "FIXTURE=%~1"
set "LOG_FILE=%~2"
set "REGRESS_OUT=%~3"
set "SCENE_PATH="
set "LAUNCHER_NAME="

if /I "%FIXTURE%"=="straight" (
    set "SCENE_PATH=res://test-straight.tscn"
    set "LAUNCHER_NAME=run_render_test_pair_straight_vs_curved:straight"
) else if /I "%FIXTURE%"=="curved_minimal" (
    set "SCENE_PATH=res://test-curved-minimal.tscn"
    set "LAUNCHER_NAME=run_render_test_pair_straight_vs_curved:curved_minimal"
) else (
    echo ERROR: Unsupported fixture=%FIXTURE%
    endlocal & exit /b 1
)

set "XPRIMERAY_REQUESTED_LAUNCHER=%LAUNCHER_NAME%"

echo.
echo ------------------------------------------------------------
echo Running fixture: %FIXTURE%
echo Log file: %LOG_FILE%
echo Scene path: %SCENE_PATH%
echo ------------------------------------------------------------

%GODOT_EXE% --path . --scene %SCENE_PATH% -- --render-test --render-test-fixture=%FIXTURE% --autocal=1 --shadow-eval=1 --autocal-verbose=1 --autocal-apply=0 > "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo ERROR: Godot run failed for fixture=%FIXTURE%
    echo See log: %LOG_FILE%
    endlocal & exit /b 1
)

python tools\renderhealth_regress.py "%LOG_FILE%" > "%REGRESS_OUT%" 2>&1
set "REGRESS_RC=%ERRORLEVEL%"
type "%REGRESS_OUT%"
if not "%REGRESS_RC%"=="0" (
    echo ERROR: renderhealth_regress.py failed for fixture=%FIXTURE%
    echo Parser output: %REGRESS_OUT%
    endlocal & exit /b %REGRESS_RC%
)

endlocal & exit /b 0
