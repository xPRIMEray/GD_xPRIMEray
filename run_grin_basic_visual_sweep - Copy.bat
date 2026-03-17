@echo off
setlocal

echo ========================================
echo GRIN Basic Visual Sweep
echo ========================================

cd /d C:\godot\godot_xPRIMEray
python tools\grin_basic_visual_sweep.py %*

if /I not "%NO_PAUSE%"=="1" pause
endlocal
