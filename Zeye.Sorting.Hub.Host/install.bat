@echo off
chcp 65001 >nul
rem 中文说明：将前后端发布包安装为自动启动的 Windows 服务。
setlocal EnableExtensions DisableDelayedExpansion
set "DRY_RUN="
if /i "%~1"=="--dry-run" set "DRY_RUN=-DryRun"
if not "%~1"=="" if not defined DRY_RUN goto usage
if not "%~2"=="" goto usage
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0service.ps1" -Action Install %DRY_RUN%
exit /b %errorlevel%
:usage
echo Usage: install.bat [--dry-run]
exit /b 2
