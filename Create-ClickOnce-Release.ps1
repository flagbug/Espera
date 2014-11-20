Set-StrictMode -version Latest
$ErrorActionPreference = "Stop"

Write-Host "Building Espera..." -ForegroundColor Green

# ==================================== Functions

Function GetMSBuildExe {
  [CmdletBinding()]
  $DotNetVersion = "4.0"
  $RegKey = "HKLM:\software\Microsoft\MSBuild\ToolsVersions\$DotNetVersion"
  $RegProperty = "MSBuildToolsPath"
  $MSBuildExe = Join-Path -Path (Get-ItemProperty $RegKey).$RegProperty -ChildPath "msbuild.exe"
  Return $MSBuildExe
}

Function ZipFiles($Filename, $Source)
{
   Add-Type -Assembly System.IO.Compression.FileSystem
   $CompressionLevel = [System.IO.Compression.CompressionLevel]::Optimal
   [System.IO.Compression.ZipFile]::CreateFromDirectory($Source, $Filename, $CompressionLevel, $false)
}

# ==================================== Variables

$NuGet = "$PSScriptRoot\.nuget\NuGet.exe"
$BuildPath = "$PSScriptRoot\Espera.View\bin\Release"
$ReleasesFolder = "$PSScriptRoot\ClickOnceReleases"
$PortableTempFolder = "$ReleasesFolder\EsperaPortable"

# ==================================== Build

If(Test-Path -Path $BuildPath) {
	Remove-Item -Confirm:$false "$BuildPath\*.*"
}

&($Nuget) restore Espera.sln

&(GetMSBuildExe) Espera.sln `
	/t:Clean`;Publish `
	/p:Platform="x86" `
	/p:Configuration=Release `
	/v:quiet

# ==================================== Portable

$ReleaseZip = "$ReleasesFolder\EsperaPortable.zip"

mkdir $PortableTempFolder

cp "$BuildPath\*.dll" $PortableTempFolder
cp "$BuildPath\Espera.exe" $PortableTempFolder
cp "$BuildPath\Espera.exe.config" $PortableTempFolder
cp ".\Changelog.md" "$PortableTempFolder\Changelog.txt"

ZipFiles $ReleaseZip $PortableTempFolder

# ==================================== ClickOnce

cp -r "$BuildPath\app.publish\" $ReleasesFolder
Rename-Item "$ReleasesFolder\app.publish\setup.exe" "EsperaSetup.exe"

# ==================================== Complete

Write-Host "Build complete: $ReleasesFolder" -ForegroundColor Green
