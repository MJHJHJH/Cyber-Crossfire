@echo off
setlocal EnableExtensions
set SCRIPT_DIR=%~dp0

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%gen_adapters.ps1"
exit /b %ERRORLEVEL%
