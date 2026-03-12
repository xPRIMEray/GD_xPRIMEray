@echo off
setlocal

set PROJECT_PATH=C:\godot\godot_xPRIMEray
cd /d %PROJECT_PATH%

echo ============================================
echo Running xPRIMEray BlackHole METRIC PhotonBand Pair
echo Fixture: blackhole_minimal
echo Transport: Metric_NullGeodesic
echo PhotonBand: ON then OFF
echo ============================================

set NO_PAUSE=1

set LOG_FILE=logs\blackhole_metric_photon_band_on.log
call run_render_test_blackhole_metric.bat
if errorlevel 1 (
  echo ERROR: PhotonBand ON run failed.
  exit /b 1
)

set LOG_FILE=logs\blackhole_metric_photon_band_off.log
call run_render_test_blackhole_metric.bat --blackhole-photon-band=off
if errorlevel 1 (
  echo ERROR: PhotonBand OFF run failed.
  exit /b 1
)

echo.
echo Done. Logs:
echo   logs\blackhole_metric_photon_band_on.log
echo   logs\blackhole_metric_photon_band_off.log
endlocal
