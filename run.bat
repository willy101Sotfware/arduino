@echo off
echo Compilando y ejecutando MetavixDispenser...

REM Compilar el proyecto
dotnet build -c Release

REM Ejecutar el programa
dotnet run -c Release

pause
