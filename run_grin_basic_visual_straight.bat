@echo off
setlocal

echo ========================================
echo GRIN Basic Visual Straight Test
echo fixture=grin_basic_visual_straight
echo transport=STRAIGHT
echo mode=FULL_RENDER
echo ========================================

if not defined GODOT_EXE set GODOT_EXE="C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
set PROJECT_PATH=C:\godot\godot_xPRIMEray
set "SCENE_PATH=res://test-grin-basic-visual-straight.tscn"
set "XPRIMERAY_REQUESTED_LAUNCHER=run_grin_basic_visual_straight"

cd /d %PROJECT_PATH%

%GODOT_EXE% --path . --scene %SCENE_PATH% %*

if /I not "%NO_PAUSE%"=="1" pause
endlocal
