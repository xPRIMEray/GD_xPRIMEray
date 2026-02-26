@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem README: Runs STRAIGHT then CURVED_MINIMAL with SmartScale (budget=rows_advanced, n=256), parses each log, and prints AutoCal summary blocks.

echo ============================================================
echo Running xPRIMEray SmartScale Render Test Pair (ROWS256)
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

set "RUN_SUFFIX=smartscale_rows256"
set "SMARTSCALE_ARGS=--smartscale=1 --smartscale-goal=max_hits --smartscale-no-early-stop=1 --smartscale-budget=rows_advanced --smartscale-budget-n=256"

set "STRAIGHT_LOG=logs\render_test_straight_%RUN_SUFFIX%_%TIMESTAMP%.txt"
set "CURVED_LOG=logs\render_test_curved_minimal_%RUN_SUFFIX%_%TIMESTAMP%.txt"
set "STRAIGHT_REGRESS_OUT=logs\renderhealth_regress_straight_%RUN_SUFFIX%_%TIMESTAMP%.txt"
set "CURVED_REGRESS_OUT=logs\renderhealth_regress_curved_minimal_%RUN_SUFFIX%_%TIMESTAMP%.txt"

call :run_fixture straight "%STRAIGHT_LOG%" "%STRAIGHT_REGRESS_OUT%"
if errorlevel 1 goto :fail

call :run_fixture curved_minimal "%CURVED_LOG%" "%CURVED_REGRESS_OUT%"
if errorlevel 1 goto :fail

echo.
echo ============================================================
echo Final Summary
echo ============================================================
echo Straight log: %STRAIGHT_LOG%
echo Straight regress: %STRAIGHT_REGRESS_OUT%
echo Curved minimal log: %CURVED_LOG%
echo Curved minimal regress: %CURVED_REGRESS_OUT%
echo.
echo Note: tools\renderhealth_summary.csv and tools\renderhealth_summary.json may be overwritten by the second parser run.
echo.
echo Finished SmartScale paired render tests (ROWS256).
endlocal
goto :eof

:fail
echo.
echo FAILED SmartScale paired render tests (ROWS256).
endlocal
exit /b 1

:run_fixture
setlocal
set "FIXTURE=%~1"
set "LOG_FILE=%~2"
set "REGRESS_OUT=%~3"

echo.
echo ------------------------------------------------------------
echo Running fixture: %FIXTURE%
echo Log file: %LOG_FILE%
echo ------------------------------------------------------------

%GODOT_EXE% --path . -- --render-test-fixture=%FIXTURE% --autocal=1 --shadow-eval=1 --autocal-verbose=1 --autocal-apply=0 %SMARTSCALE_ARGS% > "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo ERROR: Godot run failed for fixture=%FIXTURE%
    echo See log: %LOG_FILE%
    endlocal & exit /b 1
)

python tools\renderhealth_regress.py "%LOG_FILE%" > "%REGRESS_OUT%" 2>&1
set "REGRESS_RC=%ERRORLEVEL%"
type "%REGRESS_OUT%"
echo.
echo ============================================================
echo AutoCal Shadow Eval Summary (%FIXTURE%)
echo Source: %REGRESS_OUT%
echo ============================================================
powershell -NoProfile -Command "$found=$false; $inBlock=$false; Get-Content -LiteralPath '%REGRESS_OUT%' | ForEach-Object { if ((-not $inBlock) -and $_ -like '=== AutoCal Shadow Eval Summary*') { $inBlock=$true; $found=$true }; if ($inBlock) { $_ }; if ($inBlock -and $_ -eq '') { break } }; if (-not $found) { Write-Host '(summary block not found in parser output)' }"
echo.
if not "%REGRESS_RC%"=="0" (
    echo ERROR: renderhealth_regress.py failed for fixture=%FIXTURE%
    echo Parser output: %REGRESS_OUT%
    endlocal & exit /b %REGRESS_RC%
)

endlocal & exit /b 0

