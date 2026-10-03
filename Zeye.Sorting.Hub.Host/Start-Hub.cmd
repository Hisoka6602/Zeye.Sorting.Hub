@echo off
rem Start the bundled Windows application from its own deployment directory.
setlocal
cd /d "%~dp0"
if not exist "wwwroot\index.html" (
    echo The bundled frontend is missing. Publish the complete Windows package first.
    exit /b 1
)
if not defined ASPNETCORE_ENVIRONMENT set "ASPNETCORE_ENVIRONMENT=Production"
"%~dp0Zeye.Sorting.Hub.Host.exe" %*
exit /b %errorlevel%
