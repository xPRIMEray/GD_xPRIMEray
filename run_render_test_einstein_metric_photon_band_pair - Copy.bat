@echo off
setlocal

set PROJECT_PATH=C:\godot\godot_xPRIMEray
cd /d %PROJECT_PATH%

echo ============================================
echo Running xPRIMEray Einstein METRIC PhotonBand Pair
echo Fixture: einstein_ring_minimal
echo Transport: Metric_NullGeodesic
echo PhotonBand: ON then OFF
echo ============================================

set NO_PAUSE=1

set LOG_FILE=logs\einstein_metric_photon_band_on.log
call run_render_test_einstein_metric.bat
if errorlevel 1 (
  echo ERROR: PhotonBand ON run failed.
  exit /b 1
)

set LOG_FILE=logs\einstein_metric_photon_band_off.log
call run_render_test_einstein_metric.bat --einstein-photon-band=off
if errorlevel 1 (
  echo ERROR: PhotonBand OFF run failed.
  exit /b 1
)

echo.
echo Done. Logs:
echo   logs\einstein_metric_photon_band_on.log
echo   logs\einstein_metric_photon_band_off.log
endlocal
