@echo off
setlocal

:menu
cls
echo ========================================
echo GRIN Basic Visual Menu
echo mode=FULL_RENDER
echo ========================================
echo 1 - Straight baseline
echo 2 - GRIN baseline
echo 3 - Exit
echo.

choice /c 123 /n /m "Select option [1-3]: "

if errorlevel 3 goto :exit
if errorlevel 2 goto :grin
if errorlevel 1 goto :straight
goto :menu

:straight
call "%~dp0run_grin_basic_visual_straight.bat" %*
goto :menu

:grin
call "%~dp0run_grin_basic_visual.bat" %*
goto :menu

:exit
endlocal
