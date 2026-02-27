@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Repeat harness: runs SmartScale fixture matrix repeatedly and appends parsed outputs.

if not defined PROJECT_PATH set "PROJECT_PATH=C:\godot\godot_xPRIMEray"
if not defined GODOT_EXE set "GODOT_EXE=C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
if not defined REPEAT_N set "REPEAT_N=5"
if not defined BUDGET_MODE set "BUDGET_MODE=renderstep_calls"
if not defined BUDGET_N (
    if /I "%BUDGET_MODE%"=="renderstep_calls" (
        set "BUDGET_N=60"
    ) else (
        set "BUDGET_N=512"
    )
)

cd /d "%PROJECT_PATH%"
if errorlevel 1 (
    echo ERROR: Failed to cd to PROJECT_PATH=%PROJECT_PATH%
    exit /b 1
)

if not exist logs mkdir logs

set "CSV_FILE=logs\smartscale_runs.csv"
set "JSONL_FILE=logs\smartscale_runs.jsonl"
set "SUMMARY_FILE=logs\smartscale_runs_summary.txt"
set "SMARTSCALE_ARGS=--smartscale=1 --smartscale-goal=max_hits --smartscale-no-early-stop=1 --smartscale-budget=%BUDGET_MODE% --smartscale-budget-n=%BUDGET_N%"

if not exist "%CSV_FILE%" (
    python tools\smartscale_extract.py --print-header=1 NUL > "%CSV_FILE%"
    if errorlevel 1 (
        echo ERROR: Failed to initialize CSV header in %CSV_FILE%
        exit /b 1
    )
) else (
    for %%I in ("%CSV_FILE%") do (
        if %%~zI EQU 0 (
            python tools\smartscale_extract.py --print-header=1 NUL > "%CSV_FILE%"
            if errorlevel 1 (
                echo ERROR: Failed to initialize CSV header in %CSV_FILE%
                exit /b 1
            )
        )
    )
)

echo ============================================================
echo Running SmartScale repeat matrix
echo ============================================================
echo REPEAT_N=%REPEAT_N%
echo BUDGET_MODE=%BUDGET_MODE%
echo BUDGET_N=%BUDGET_N%
echo CSV=%CSV_FILE%
echo JSONL=%JSONL_FILE%
echo.

for /L %%R in (1,1,%REPEAT_N%) do (
    call :run_fixture straight %%R
    if errorlevel 1 goto :fail

    call :run_fixture curved_minimal %%R
    if errorlevel 1 goto :fail
)

python tools\smartscale_analyze.py "%CSV_FILE%" > "%SUMMARY_FILE%"
if errorlevel 1 (
    echo ERROR: Failed to generate summary: %SUMMARY_FILE%
    goto :fail
)

echo ============================================================
echo SmartScale repeat summary
echo ============================================================
type "%SUMMARY_FILE%"
echo.
echo Completed SmartScale repeat matrix.
endlocal
exit /b 0

:run_fixture
setlocal EnableExtensions DisableDelayedExpansion
set "FIXTURE=%~1"
set "REPEAT_IDX=%~2"
set "RUN_TS="
for /f %%T in ('powershell -NoProfile -Command "Get-Date -Format \"yyyy-MM-dd_HH-mm-ss-fff\""') do set "RUN_TS=%%T"
if not defined RUN_TS (
    set "RUN_TS=%DATE%_%TIME%"
    set "RUN_TS=%RUN_TS:/=-%"
    set "RUN_TS=%RUN_TS:\=-%"
    set "RUN_TS=%RUN_TS::=-%"
    set "RUN_TS=%RUN_TS:.=-%"
    set "RUN_TS=%RUN_TS:,=-%"
    set "RUN_TS=%RUN_TS: =0%"
)

set "LOG_FILE=logs\render_test_%FIXTURE%_smartscale_%BUDGET_MODE%_%BUDGET_N%_r%REPEAT_IDX%_%RUN_TS%.txt"

echo ------------------------------------------------------------
echo Running fixture=%FIXTURE% repeat=%REPEAT_IDX%
echo Log=%LOG_FILE%
echo ------------------------------------------------------------

"%GODOT_EXE%" --path . -- --render-test-fixture=%FIXTURE% --autocal=1 --shadow-eval=1 --autocal-verbose=1 --autocal-apply=0 %SMARTSCALE_ARGS% > "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo ERROR: Godot run failed fixture=%FIXTURE% repeat=%REPEAT_IDX%
    echo FAILED_LOG=%LOG_FILE%
    endlocal & set "FAILED_FIXTURE=%FIXTURE%" & set "FAILED_REPEAT=%REPEAT_IDX%" & set "FAILED_LOG=%LOG_FILE%" & exit /b 1
)

python tools\smartscale_extract.py "%LOG_FILE%" >> "%CSV_FILE%"
if errorlevel 1 (
    echo ERROR: CSV extraction failed fixture=%FIXTURE% repeat=%REPEAT_IDX%
    echo FAILED_LOG=%LOG_FILE%
    endlocal & set "FAILED_FIXTURE=%FIXTURE%" & set "FAILED_REPEAT=%REPEAT_IDX%" & set "FAILED_LOG=%LOG_FILE%" & exit /b 1
)

python tools\smartscale_extract.py --json "%LOG_FILE%" >> "%JSONL_FILE%"
if errorlevel 1 (
    echo ERROR: JSONL extraction failed fixture=%FIXTURE% repeat=%REPEAT_IDX%
    echo FAILED_LOG=%LOG_FILE%
    endlocal & set "FAILED_FIXTURE=%FIXTURE%" & set "FAILED_REPEAT=%REPEAT_IDX%" & set "FAILED_LOG=%LOG_FILE%" & exit /b 1
)

echo Completed fixture=%FIXTURE% repeat=%REPEAT_IDX%
echo.
endlocal & exit /b 0

:fail
echo.
echo FAILED SmartScale repeat matrix.
if defined FAILED_FIXTURE echo Fixture=%FAILED_FIXTURE%
if defined FAILED_REPEAT echo Repeat=%FAILED_REPEAT%
if defined FAILED_LOG echo Log=%FAILED_LOG%
endlocal
exit /b 1
