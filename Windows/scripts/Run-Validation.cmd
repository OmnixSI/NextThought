@echo off
setlocal
"%~dp0tools\NextThought.Cli.exe" doctor --audio "%~dp0tools\validation\public-sample.wav"
set "validationExit=%errorlevel%"
if not "%NEXTTHOUGHT_VALIDATION_NO_PAUSE%"=="1" pause
exit /b %validationExit%
