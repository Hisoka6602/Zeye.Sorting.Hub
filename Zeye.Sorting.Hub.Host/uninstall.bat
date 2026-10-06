@echo off
chcp 65001 >nul
rem 中文说明：停止并卸载 Windows 服务，保留发布文件与数据。
setlocal EnableExtensions DisableDelayedExpansion
set "DRY_RUN="
if /i "%~1"=="--dry-run" set "DRY_RUN=-DryRun"
if not "%~1"=="" if not defined DRY_RUN goto usage
if not "%~2"=="" goto usage
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0service.ps1" -Action Uninstall %DRY_RUN%
exit /b %errorlevel%
:usage
echo Usage: uninstall.bat [--dry-run]
exit /b 2
