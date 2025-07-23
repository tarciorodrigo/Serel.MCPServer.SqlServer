@echo off
echo Building SQL Server MCP Server...

REM Limpar build anterior
dotnet clean > nul 2>&1

REM Restaurar dependências
echo Restoring packages...
dotnet restore

REM Build do projeto
echo Building project...
dotnet build --configuration Release --no-restore

REM Verificar se o build foi bem-sucedido
if %ERRORLEVEL% EQU 0 (
    echo Build completed successfully!
    echo.
    echo To run the server:
    echo dotnet run --configuration Release
    echo.
    echo Or publish for deployment:
    echo dotnet publish --configuration Release --output ./publish
) else (
    echo Build failed with error code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

pause