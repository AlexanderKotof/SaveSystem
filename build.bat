@echo off
setlocal

:: Change to the script's directory (project root)
cd /d "%~dp0"

echo Working directory: %CD%
echo Building project from folder: SourceGenerator
echo Output path: SaveSystem\Assets\Generator
echo ----------------------------------------

:: Run the build
dotnet build "SaveSystem.SourceGenerator" -c Release -o "SaveSystem\Assets\Generator"

:: Check the result
if %errorlevel% neq 0 (
    echo ERROR: Build failed with code %errorlevel%
    pause
    exit /b %errorlevel%
) else (
    echo Build completed successfully!
)

pause
