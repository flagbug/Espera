#r "./packages/FAKE.3.17.14/tools/Fakelib.dll"
open Fake

trace "Building Espera..."

RestorePackages()

let solution = "./Espera.sln"
let buildDir = "./Espera.View/bin/Release"
let releasesDir = "./Releases"
let setupIcon = "./Espera.View/Images/ApplicationIcon.ico"
let portableDir = releasesDir @@ "EsperaPortable"

Target "Clean" (fun _ ->
    CleanDirs [releasesDir; buildDir]
)

Target "Build" (fun _ ->
    MSBuildRelease null "Rebuild" [solution]
        |> Log "Build-Output: "
)

"Clean"
    ==> "Build"

RunTargetOrDefault "Build"