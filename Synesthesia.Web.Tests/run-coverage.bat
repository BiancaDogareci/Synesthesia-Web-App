@echo off
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:ExcludeByFile="**/Areas/Identity/**/*.cs%%2c**/Migrations/**/*.cs%%2c**/*.cshtml.g.cs%%2c**/Program.cs%%2c**/Pages/Shared/**/*.cs" /p:ExcludeByAttribute="GeneratedCodeAttribute%%2cCompilerGeneratedAttribute%%2cExcludeFromCodeCoverageAttribute"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Generating HTML report...
    reportgenerator -reports:"./TestResults/coverage.cobertura.xml" -targetdir:"./TestResults/CoverageReport" -reporttypes:Html
    
    echo.
    echo Opening coverage report...
    start ./TestResults/CoverageReport/index.html
)
