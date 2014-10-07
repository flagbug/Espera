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
&($Nuget) restore Espera.sln
$Squirrel = Join-Path  (ls .\packages\squirrel.windows.*)[0] "tools\Squirrel.com"

$BuildPath = "$PSScriptRoot\Espera.View\bin\Release"
$NuSpecPath = "$PSScriptRoot\Espera.nuspec"
$ReleasesFolder = "$PSScriptRoot\SquirrelReleases"

# ==================================== NuSpec Metadata

$NuSpecXml = [xml](Get-Content $NuSpecPath)
$Version = $NuSpecXml.package.metadata.version

# ==================================== Build

If(Test-Path -Path $BuildPath) {
	Remove-Item -Confirm:$false "$BuildPath\*.*"
}

&(GetMSBuildExe) Espera.sln `
	/t:Clean`;Rebuild `
	/p:Configuration=Release `
	/p:AllowedReferenceRelatedFileExtensions=- `
	/p:DebugSymbols=false `
	/p:DebugType=None `
	/clp:ErrorsOnly `
	/v:m

# ==================================== Portable

$ReleaseZip = "$ReleasesFolder\EsperaPortable.zip"

If(!(Test-Path -Path $ReleasesFolder )){
    New-Item -ItemType directory -Path $ReleasesFolder
}

If(Test-Path -Path $ReleaseZip) {
	Remove-Item -Confirm:$false $ReleaseZip
}

ZipFiles $ReleaseZip $BuildPath

# ==================================== Squirrel

$NuPkgPath = "$PSScriptRoot\Espera.$Version.nupkg"

&($NuGet) pack $NuSpecPath

$SquirrelFullNuPkgOutputPath = "$PSScriptRoot\Releases\Espera-$Version-full.nupkg"
If(Test-Path -Path $SquirrelFullNuPkgOutputPath) {
	Remove-Item -Confirm:$false $SquirrelFullNuPkgOutputPath
}

$SquirrelDeltaNuPkgOutputPath = "$PSScriptRoot\Releases\Espera-$Version-delta.nupkg"
If(Test-Path -Path $SquirrelDeltaNuPkgOutputPath) {
	Remove-Item -Confirm:$false $SquirrelDeltaNuPkgOutputPath
}

$OutputSetupExe = "$ReleasesFolder\Espera.Setup.$Version.exe"
If(Test-Path -Path $OutputSetupExe) {
	Remove-Item -Confirm:$false $OutputSetupExe
}

&($Squirrel) --releasify $NuPkgPath

$SquirrelSetupExe = "$ReleasesFolder\Setup.exe"
If(Test-Path -Path $SquirrelSetupExe) {
	Rename-Item $SquirrelSetupExe $OutputSetupExe
}

# ==================================== Cleanup

If(Test-Path -Path $NuPkgPath) {
	Remove-Item -Confirm:$false $NuPkgPath
}

# ==================================== Complete

Write-Host "Build $Version complete: $ReleasesFolder" -ForegroundColor Green
