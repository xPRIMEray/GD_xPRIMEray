@echo off
setlocal

set PROJECT_PATH=C:\godot\godot_xPRIMEray
cd /d %PROJECT_PATH%

echo ============================================
echo Running xPRIMEray Einstein FAST COMPARE PAIR
echo Fixture: einstein_ring_minimal
echo Transport models: GRIN_Optical then Metric_NullGeodesic
echo ============================================

set NO_PAUSE=1
call run_render_test_einstein_grin_fast_compare.bat
if errorlevel 1 (
  echo ERROR: GRIN fast compare run failed.
  exit /b 1
)

call run_render_test_einstein_metric_fast_compare.bat
if errorlevel 1 (
  echo ERROR: METRIC fast compare run failed.
  exit /b 1
)

echo.
echo Done. Logs:
echo   logs\ab_einstein_grin_fast_compare.log
echo   logs\ab_einstein_metric_fast_compare.log
endlocal
