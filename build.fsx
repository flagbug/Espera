#r "./packages/FAKE.3.17.14/tools/Fakelib.dll"
open Fake

trace "Building Espera..."

RestorePackages()

let solution = "./Espera.sln"
let buildDir = "./Espera.View/bin/Release"
let releasesDir = "./Releases"
let setupIcon = "./Espera.View/Images/ApplicationIcon.ico"
let portableDir = releasesDir @@ "EsperaPortable"
let nuget = "./.nuget/Nuget.exe"
let squirrelPath = findToolFolderInSubPath "./packages/**/Squirrel.exe" null
let squirrel = squirrelPath @@ "Squirrel.exe"
let squirrelUpdate = squirrelPath @@ "Squirrel.exe"
let squirrelSync = squirrelPath @@ "SyncReleases.exe"
let nuspecPath = "./Espera.nuspec"
let nuspecData = getNuspecProperties (ReadFileAsString nuspecPath)
let nugetPackage = "./Espera." + nuspecData.Version + ".nupkg"

Target "Clean" (fun _ ->
    CleanDirs [releasesDir; buildDir]
)

Target "Build" (fun _ ->
    MSBuildRelease null "Rebuild" [solution]
        |> Log "Build-Output: "
)

Target "Squirrel" (fun _ ->
    let includeUpdater = getBuildParamOrDefault "includeUpdater" "false"
    
    if includeUpdater = "true" then
        CopyFile squirrelUpdate buildDir
        
    let syncResult = ExecProcess (fun info ->
        info.FileName <- squirrelSync 
        info.Arguments <- "-r " + releasesDir + " -u http://getespera.com/releases/squirrel/") (System.TimeSpan.FromMinutes 5.0)

    if syncResult <> 0 then failwithf "Squirrel.exe returned with a non-zero exit code"
    
    let packResult = ExecProcess (fun info ->
        info.FileName <- nuget
        info.WorkingDirectory <- "./"
        info.Arguments <- "pack " + nuspecPath) (System.TimeSpan.FromMinutes 5.0)

    if syncResult <> 0 then failwithf "NuGet.exe returned with a non-zero exit code"
    
    let certPassword = getBuildParamOrDefault "certpass" ""
    
    if syncResult <> 0 then failwithf "NuGet.exe returned with a non-zero exit code"
)

Target "Default" (fun _ ->
    trace "Build"
)
    
"Clean"
    ==> "Build"
    ==> "Squirrel"
    ==> "Default"
    
RunTargetOrDefault "Default"