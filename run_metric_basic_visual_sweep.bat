@echo off
setlocal

echo ========================================
echo Metric Basic Visual Sweep
echo ========================================

cd /d C:\godot\godot_xPRIMEray
python tools\metric_basic_visual_sweep.py %*

if /I not "%NO_PAUSE%"=="1" pause
endlocal
