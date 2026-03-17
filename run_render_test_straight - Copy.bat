@echo off
setlocal

echo ============================================
echo Running xPRIMEray Render Test (STRAIGHT)
echo ============================================

set GODOT_EXE="C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
set PROJECT_PATH=C:\godot\godot_xPRIMEray
set LOG_FILE=render_test_log_straight.txt
set "SCENE_PATH=res://test-straight.tscn"
set "XPRIMERAY_REQUESTED_LAUNCHER=run_render_test_straight"

cd /d %PROJECT_PATH%

%GODOT_EXE% --path . --scene %SCENE_PATH% -- --render-test --render-test-fixture=straight > %LOG_FILE% 2>&1

python tools\renderhealth_regress.py %LOG_FILE%

echo.
echo Finished STRAIGHT test.
pause
endlocal
