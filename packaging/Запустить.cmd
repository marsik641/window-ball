@echo off
cd /d "%~dp0"
if not exist "WindowBall.exe" (
    echo WindowBall.exe not found. Extract the entire portable archive first.
    pause
    exit /b 1
)
start "" "%~dp0WindowBall.exe"
