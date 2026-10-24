@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 10 SDK was not found.
  echo Install it from https://dotnet.microsoft.com/download/dotnet/10.0 and run this script again.
  pause
  exit /b 2
)
dotnet publish "src\I-Total.Transfer\I-Total.Transfer.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "publish"
if errorlevel 1 (
  echo Build failed. Review the errors above.
  pause
  exit /b 1
)
echo.
echo Build completed: publish\I-Total.Transfer.exe
pause
