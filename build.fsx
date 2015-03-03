#r "./packages/FAKE.3.17.14/tools/Fakelib.dll"
open Fake

trace "Building Espera..."

RestorePackages()

let solution = "./Espera.sln"
let buildDir = "./Espera.View/bin/Release"
let releasesDir = "./Releases"
let setupIcon = "./Espera.View/Images/ApplicationIcon.ico"
let portableDir = releasesDir @@ "EsperaPortable"
let squirrelPath = findToolFolderInSubPath "./packages/**/Squirrel.exe" null
let squirrel = squirrelPath @@ "Squirrel.exe"
let squirrelUpdate = squirrelPath @@ "Squirrel.exe"
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
        Copy squirrelUpdate buildDir
)

"Clean"
    ==> "Build"

RunTargetOrDefault "Build"