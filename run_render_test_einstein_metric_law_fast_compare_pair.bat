@echo off
setlocal

set PROJECT_PATH=C:\godot\godot_xPRIMEray
cd /d %PROJECT_PATH%

echo ============================================
echo Running xPRIMEray Einstein METRIC LAW FAST COMPARE
echo Fixture: einstein_ring_minimal
echo Metric laws: current envelope then impact-parameter approx
echo ============================================

set NO_PAUSE=1
call run_render_test_einstein_metric_fast_compare.bat --metric-law=current
if errorlevel 1 (
  echo ERROR: current metric law run failed.
  exit /b 1
)
copy /Y logs\ab_einstein_metric_fast_compare.log logs\ab_einstein_metric_current_fast_compare.log >nul

call run_render_test_einstein_metric_fast_compare.bat --metric-law=impact
if errorlevel 1 (
  echo ERROR: impact-parameter metric law run failed.
  exit /b 1
)
copy /Y logs\ab_einstein_metric_fast_compare.log logs\ab_einstein_metric_impact_fast_compare.log >nul

echo.
echo Done. Logs:
echo   logs\ab_einstein_metric_current_fast_compare.log
echo   logs\ab_einstein_metric_impact_fast_compare.log
endlocal
