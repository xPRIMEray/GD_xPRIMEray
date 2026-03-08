@echo off
setlocal

echo ============================================
echo Running xPRIMEray Render Test (BLACKHOLE METRIC FAST COMPARE)
echo ============================================

set GODOT_EXE="C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
set PROJECT_PATH=C:\godot\godot_xPRIMEray
set LOG_FILE=logs\ab_blackhole_metric_fast_compare.log

cd /d %PROJECT_PATH%

%GODOT_EXE% --path . -- --render-test --render-test-fixture=blackhole_minimal --render-test-profile=blackhole_compare_fast --blackhole-transport-model=metric --lifecycle-stress=0 --smartscale=0 > %LOG_FILE% 2>&1

python tools\renderhealth_regress.py %LOG_FILE%

echo.
echo Finished BLACKHOLE METRIC fast comparison test.
pause
endlocal
