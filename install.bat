@echo off
echo Instalando dependencias...

REM Instalar .NET SDK si no está instalado
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo Instalando .NET SDK...
    powershell -Command "& { Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'dotnet-install.ps1'; ./dotnet-install.ps1 }"
)

REM Instalar drivers FTDI si no están instalados
if not exist "C:\Windows\System32\FTD2XX.dll" (
    echo Instalando drivers FTDI...
    powershell -Command "& { Invoke-WebRequest 'https://ftdichip.com/wp-content/uploads/2021/02/CDM21228_Setup.zip' -OutFile 'ftdi_drivers.zip'; Expand-Archive ftdi_drivers.zip .; .\CDM21228_Setup.exe }"
)

REM Restaurar paquetes NuGet
dotnet restore

echo Instalación completada.
pause
