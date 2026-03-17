@echo off
setlocal

:menu
cls
echo ========================================
echo GRIN Basic Visual Menu
echo mode=FULL_RENDER
echo ========================================
echo 1 - Straight baseline
echo 2 - Curved minimal
echo 3 - Curved stronger
echo 4 - Exit
echo.

choice /c 1234 /n /m "Select option [1-4]: "

if errorlevel 4 goto :exit
if errorlevel 3 goto :grin
if errorlevel 2 goto :minimal
if errorlevel 1 goto :straight
goto :menu

:straight
call "%~dp0run_grin_basic_visual_straight.bat" %*
goto :menu

:minimal
call "%~dp0run_grin_basic_visual_minimal.bat" %*
goto :menu

:grin
call "%~dp0run_grin_basic_visual.bat" %*
goto :menu

:exit
endlocal
