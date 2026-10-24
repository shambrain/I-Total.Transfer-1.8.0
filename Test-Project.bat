@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 10 SDK was not found. Install it from https://dotnet.microsoft.com/download/dotnet/10.0
  pause
  exit /b 2
)
dotnet test "tests\I-Total.Transfer.Tests\I-Total.Transfer.Tests.csproj" -c Release
if errorlevel 1 (
  echo Tests failed. Review the errors above.
  pause
  exit /b 1
)
echo All unit tests passed.
pause
