@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem README: Runs the SmartScale matrix (straight/curved_minimal x calls60/rows256), parses each log, and prints AutoCal summary blocks after each run.

echo ============================================================
echo Running xPRIMEray SmartScale Render Test Matrix
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

set "STRAIGHT_CALLS60_LOG=logs\render_test_straight_smartscale_calls60_%TIMESTAMP%.txt"
set "STRAIGHT_CALLS60_REGRESS=logs\renderhealth_regress_straight_smartscale_calls60_%TIMESTAMP%.txt"
set "CURVED_CALLS60_LOG=logs\render_test_curved_minimal_smartscale_calls60_%TIMESTAMP%.txt"
set "CURVED_CALLS60_REGRESS=logs\renderhealth_regress_curved_minimal_smartscale_calls60_%TIMESTAMP%.txt"
set "STRAIGHT_ROWS256_LOG=logs\render_test_straight_smartscale_rows256_%TIMESTAMP%.txt"
set "STRAIGHT_ROWS256_REGRESS=logs\renderhealth_regress_straight_smartscale_rows256_%TIMESTAMP%.txt"
set "CURVED_ROWS256_LOG=logs\render_test_curved_minimal_smartscale_rows256_%TIMESTAMP%.txt"
set "CURVED_ROWS256_REGRESS=logs\renderhealth_regress_curved_minimal_smartscale_rows256_%TIMESTAMP%.txt"

set "SMARTSCALE_LABEL=smartscale_calls60"
set "SMARTSCALE_ARGS=--smartscale=1 --smartscale-goal=max_hits --smartscale-no-early-stop=1 --smartscale-budget=renderstep_calls --smartscale-budget-n=60"
call :run_fixture straight "%STRAIGHT_CALLS60_LOG%" "%STRAIGHT_CALLS60_REGRESS%"
if errorlevel 1 goto :fail

call :run_fixture curved_minimal "%CURVED_CALLS60_LOG%" "%CURVED_CALLS60_REGRESS%"
if errorlevel 1 goto :fail

set "SMARTSCALE_LABEL=smartscale_rows256"
set "SMARTSCALE_ARGS=--smartscale=1 --smartscale-goal=max_hits --smartscale-no-early-stop=1 --smartscale-budget=rows_advanced --smartscale-budget-n=256"
call :run_fixture straight "%STRAIGHT_ROWS256_LOG%" "%STRAIGHT_ROWS256_REGRESS%"
if errorlevel 1 goto :fail

call :run_fixture curved_minimal "%CURVED_ROWS256_LOG%" "%CURVED_ROWS256_REGRESS%"
if errorlevel 1 goto :fail

echo.
echo ============================================================
echo Final Summary
echo ============================================================
echo 1) straight calls60 log: %STRAIGHT_CALLS60_LOG%
echo 1) straight calls60 regress: %STRAIGHT_CALLS60_REGRESS%
echo 2) curved_minimal calls60 log: %CURVED_CALLS60_LOG%
echo 2) curved_minimal calls60 regress: %CURVED_CALLS60_REGRESS%
echo 3) straight rows256 log: %STRAIGHT_ROWS256_LOG%
echo 3) straight rows256 regress: %STRAIGHT_ROWS256_REGRESS%
echo 4) curved_minimal rows256 log: %CURVED_ROWS256_LOG%
echo 4) curved_minimal rows256 regress: %CURVED_ROWS256_REGRESS%
echo.
echo Note: tools\renderhealth_summary.csv and tools\renderhealth_summary.json may be overwritten by later parser runs.
echo.
echo Finished SmartScale matrix runs.
endlocal
goto :eof

:fail
echo.
echo FAILED SmartScale matrix runs.
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
echo Mode: %SMARTSCALE_LABEL%
echo Log file: %LOG_FILE%
echo ------------------------------------------------------------

%GODOT_EXE% --path . -- --render-test-fixture=%FIXTURE% --autocal=1 --shadow-eval=1 --autocal-verbose=1 --autocal-apply=0 %SMARTSCALE_ARGS% > "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo ERROR: Godot run failed for fixture=%FIXTURE% mode=%SMARTSCALE_LABEL%
    echo See log: %LOG_FILE%
    endlocal & exit /b 1
)

echo ============================================================
echo Post-Run Log Tail (%FIXTURE%, %SMARTSCALE_LABEL%)
echo Source: %LOG_FILE%
echo ============================================================
powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG_FILE%' -Tail 40"
echo.
echo [Extract] [SmartScaleResult]
powershell -NoProfile -Command "$m = Select-String -LiteralPath '%LOG_FILE%' -Pattern '\[SmartScaleResult\]'; if ($m) { $m | Select-Object -Last 1 | ForEach-Object { $_.Line } } else { Write-Host '(not found)' }"
echo [Extract] [SmartScale][Summary]
powershell -NoProfile -Command "$m = Select-String -LiteralPath '%LOG_FILE%' -Pattern '\[SmartScale\]\[Summary\]'; if ($m) { $m | ForEach-Object { $_.Line } } else { Write-Host '(not found)' }"
echo [Extract] last 6 [SmartScale][ProbeResult]
powershell -NoProfile -Command "$m = Select-String -LiteralPath '%LOG_FILE%' -Pattern '\[SmartScale\]\[ProbeResult\]'; if ($m) { $m | Select-Object -Last 6 | ForEach-Object { $_.Line } } else { Write-Host '(not found)' }"
echo [Extract] last 6 [AutoCalShadowEval] decisions
powershell -NoProfile -Command "$m = Select-String -LiteralPath '%LOG_FILE%' -Pattern '\[RenderTestRunner\]\[AutoCalShadowEval\].*decision='; if ($m) { $m | Select-Object -Last 6 | ForEach-Object { $_.Line } } else { Write-Host '(not found)' }"
echo.

python tools\renderhealth_regress.py "%LOG_FILE%" > "%REGRESS_OUT%" 2>&1
set "REGRESS_RC=%ERRORLEVEL%"
type "%REGRESS_OUT%"
echo.
echo ============================================================
echo AutoCal Shadow Eval Summary (%FIXTURE%, %SMARTSCALE_LABEL%)
echo Source: %REGRESS_OUT%
echo ============================================================
powershell -NoProfile -Command "$found=$false; $inBlock=$false; Get-Content -LiteralPath '%REGRESS_OUT%' | ForEach-Object { if ((-not $inBlock) -and $_ -like '=== AutoCal Shadow Eval Summary*') { $inBlock=$true; $found=$true }; if ($inBlock) { $_ }; if ($inBlock -and $_ -eq '') { break } }; if (-not $found) { Write-Host '(summary block not found in parser output)' }"
echo.
if not "%REGRESS_RC%"=="0" (
    echo ERROR: renderhealth_regress.py failed for fixture=%FIXTURE% mode=%SMARTSCALE_LABEL%
    echo Parser output: %REGRESS_OUT%
    endlocal & exit /b %REGRESS_RC%
)

endlocal & exit /b 0

