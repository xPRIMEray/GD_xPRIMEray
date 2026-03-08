@echo off
setlocal

echo ============================================
echo Running xPRIMEray Render Test (EINSTEIN METRIC)
echo ============================================

set GODOT_EXE="C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
set PROJECT_PATH=C:\godot\godot_xPRIMEray
set LOG_FILE=render_test_log_einstein_metric.txt

cd /d %PROJECT_PATH%

%GODOT_EXE% --path . -- --render-test --render-test-fixture=einstein_ring_minimal --einstein-transport-model=metric --lifecycle-stress=0 --smartscale=0 > %LOG_FILE% 2>&1

python tools\renderhealth_regress.py %LOG_FILE%

echo.
echo Finished EINSTEIN METRIC test.
pause
endlocal
