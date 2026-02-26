@echo off
setlocal

echo ============================================
echo Running xPRIMEray Render Test (CURVED MINIMAL)
echo ============================================

set GODOT_EXE="C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
set PROJECT_PATH=C:\godot\godot_xPRIMEray
set LOG_FILE=render_test_log_curved_minimal.txt

cd /d %PROJECT_PATH%

%GODOT_EXE% --path . -- --render-test-fixture=curved_minimal > %LOG_FILE% 2>&1

python tools\renderhealth_regress.py %LOG_FILE%

echo.
echo Finished CURVED MINIMAL test.
pause
endlocal
