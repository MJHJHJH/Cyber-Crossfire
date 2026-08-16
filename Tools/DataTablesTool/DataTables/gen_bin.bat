@echo off
setlocal EnableExtensions
set SCRIPT_DIR=%~dp0
set LUBAN_DLL=%SCRIPT_DIR%..\Luban\Luban.dll
set CONF_ROOT=%SCRIPT_DIR%
set PROJECT_ROOT=%SCRIPT_DIR%..\..\..
set CODE_OUT=%PROJECT_ROOT%\Assets\DataTable\Core\Bin
set DATA_OUT=%PROJECT_ROOT%\Assets\DataTable\Data\Bin

echo [gen_bin] cleaning ...
call :CleanDataDir "%DATA_OUT%"
call :CleanCodeDir "%CODE_OUT%"

echo [gen_bin] generating ...
dotnet "%LUBAN_DLL%" ^
    -t client-bin ^
    -c cs-bin ^
    -d bin ^
    --conf "%CONF_ROOT%luban.conf" ^
    -x outputCodeDir=%CODE_OUT% ^
    -x outputDataDir=%DATA_OUT%

if %ERRORLEVEL% NEQ 0 (
    echo gen_bin failed with code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo gen_bin done.
exit /b 0

:CleanDataDir
if not exist "%~1" mkdir "%~1"
del /q "%~1\*.json" 2>nul
del /q "%~1\*.json.meta" 2>nul
del /q "%~1\*.bytes" 2>nul
del /q "%~1\*.bytes.meta" 2>nul
goto :eof

:CleanCodeDir
if not exist "%~1" mkdir "%~1"
for /d %%D in ("%~1\*") do rd /s /q "%%~D" 2>nul
del /q "%~1\*.*" 2>nul
goto :eof
