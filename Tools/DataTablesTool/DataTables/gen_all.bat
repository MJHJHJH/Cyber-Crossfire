@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ========================================
echo  Luban full generate pipeline
echo  1) Json code + data
echo  2) Bin code + data
echo  3) Adapters (I{Table} / Access / Factory)
echo ========================================
echo.

call "%~dp0gen_json.bat"
if %ERRORLEVEL% NEQ 0 (
    echo [gen_all] FAILED at gen_json
    call :MaybePause
    exit /b %ERRORLEVEL%
)

call "%~dp0gen_bin.bat"
if %ERRORLEVEL% NEQ 0 (
    echo [gen_all] FAILED at gen_bin
    call :MaybePause
    exit /b %ERRORLEVEL%
)

call "%~dp0gen_adapters.bat"
if %ERRORLEVEL% NEQ 0 (
    echo [gen_all] FAILED at gen_adapters
    call :MaybePause
    exit /b %ERRORLEVEL%
)

echo.
echo [gen_all] SUCCESS
echo   Core code : Assets\DataTable\Core\{Json,Bin,Adapters}
echo   Data files: Assets\DataTable\Data\{Json,Bin}
echo.
call :MaybePause
exit /b 0

:MaybePause
if /I "%LUBAN_NOPAUSE%"=="1" goto :eof
pause
goto :eof
