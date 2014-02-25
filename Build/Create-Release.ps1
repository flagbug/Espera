$MSBuildLocation = "C:\Program Files (x86)\MSBuild\12.0\bin"

& "$MSBuildLocation\MSBuild.exe" ..\Espera\Espera.sln /p:Configuration=Release /p:Platform="Any CPU" /t:Rebuild

$SatelliteProjects = {
    "Espera.Core", "Espera.Services"
}