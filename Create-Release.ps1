$MSBuildLocation = "C:\Program Files (x86)\MSBuild\12.0\bin"

if (Test-Path .\Release) {
    rmdir -r -force .\Release
}

mkdir .\Release
mkdir .\Release\Portable
mkdir .\Release\Publish

# Build the portable version
Write-Host "Building Portable Version"
& "$MSBuildLocation\MSBuild.exe" /t:Rebuild /p:Configuration=Release /p:Platform="x86" /v:quiet ".\Espera\Espera.sln"

cp ".\Espera\Espera.View\bin\Release\*.dll" ".\Release\Portable\"
cp ".\Espera\Espera.View\bin\Release\Espera.exe" ".\Release\Portable\"
cp ".\Espera\Espera.View\bin\Release\Espera.exe.config" ".\Release\Portable\"

# Build the ClickOnce version
Write-Host "Building ClickOnce version"
& "$MSBuildLocation\MSBuild.exe" /target:publish /t:Rebuild /p:Configuration=Release /p:Platform="x86" /v:quiet ".\Espera\Espera.sln"

cp -r ".\Espera\Espera.View\publish\" ".\Release\Publish\"