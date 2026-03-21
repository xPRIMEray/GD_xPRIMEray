@echo off
setlocal

echo ========================================
echo Renderer Compact Regression Pack
echo fixtures=3
echo summary=logs\renderer_compact_regression_pack\summary.json
echo ========================================

cd /d C:\godot\godot_xPRIMEray
python tools\renderer_compact_regression_pack.py %*

if /I not "%NO_PAUSE%"=="1" pause
endlocal
