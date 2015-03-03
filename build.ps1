$NuGet = "$PSScriptRoot\.nuget\NuGet.exe"
&($NuGet) Install FAKE -OutputDirectory packages -Version 3.17.14

$FAKE = "$PSScriptRoot\packages\FAKE.3.17.14\tools\FAKE.exe"
&($FAKE) "$PSScriptRoot\build.fsx"