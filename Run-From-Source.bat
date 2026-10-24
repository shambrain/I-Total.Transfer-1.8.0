@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0
  pause
  exit /b 2
)
dotnet run --project "src\I-Total.Transfer\I-Total.Transfer.csproj"
