#r "paket:
storage: packages
nuget FSharp.Core 4.7
nuget Fake.IO.FileSystem
nuget Fake.DotNet.Cli
nuget Fake.Core.Target
nuget Fake.Core.ReleaseNotes
nuget Fake.Tools.Git //"
#if !FAKE
#load ".fake/build.fsx/intellisense.fsx"
#r "Facades/netstandard"
#endif

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.Tools
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open System
open System.IO


let gitName = "urlParser"
let gitOwner = "elmish"
let gitHome = sprintf "https://github.com/%s" gitOwner
let gitRepo = sprintf "git@github.com:%s/%s.git" gitOwner gitName

// Filesets
let projects  =
    !! "src/**.fsproj"
    ++ "netstandard/**.fsproj"

let withWorkDir = DotNet.Options.withWorkingDirectory

Target.create "Clean" (fun _ ->
    Shell.cleanDir "src/obj"
    Shell.cleanDir "src/bin"
    Shell.cleanDir "netstandard/obj"
    Shell.cleanDir "netstandard/bin"
)

Target.create "Restore" (fun _ ->
    projects
    |> Seq.iter (fun s ->
        let dir = Path.GetDirectoryName s
        DotNet.restore (fun a -> a.WithCommon (withWorkDir dir)) s
    )
)

Target.create "Build" (fun _ ->
    projects
    |> Seq.iter (fun s ->
        let dir = Path.GetDirectoryName s
        DotNet.build (fun a ->
            a.WithCommon
                (fun c ->
                    let c = c |> withWorkDir dir
                    {c with CustomParams = Some "/p:SourceLinkCreate=true"}))
            s
    )
)

Target.create "Test" (fun _ ->
    DotNet.test (fun a -> a.WithCommon id) "tests"
)

let release = ReleaseNotes.load "RELEASE_NOTES.md"

Target.create "Meta" (fun _ ->
    [ "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
      "<PropertyGroup>"
      "<Description>UrlParser for Elmish apps</Description>"
      sprintf "<PackageProjectUrl>http://%s.github.io/%s</PackageProjectUrl>" gitOwner gitName
      "<PackageLicenseUrl>https://raw.githubusercontent.com/elmish/urlParser/master/LICENSE.md</PackageLicenseUrl>"
      "<PackageIconUrl>https://raw.githubusercontent.com/elmish/elmish/master/docs/files/img/logo.png</PackageIconUrl>"
      sprintf "<RepositoryUrl>%s/%s</RepositoryUrl>" gitHome gitName
      "<PackageTags>fable;elmish;fsharp</PackageTags>"
      sprintf "<PackageReleaseNotes>%s</PackageReleaseNotes>" (List.head release.Notes)
      "<Authors>Eugene Tolmachev</Authors>"
      sprintf "<Version>%s</Version>" (string release.SemVer)
      "</PropertyGroup>"
      "</Project>"]
    |> File.write false "Directory.Build.props"
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "Package" (fun _ ->
    projects
    |> Seq.iter (fun s ->
        let dir = Path.GetDirectoryName s
        DotNet.pack (fun a ->
            a.WithCommon (withWorkDir dir)
        ) s
    )
)

Target.create "PublishNuget" (fun _ ->
    let exec dir =
        DotNet.exec (fun a ->
            a.WithCommon (withWorkDir dir)
        )

    let args = sprintf "push Fable.Elmish.UrlParser.%s.nupkg -s nuget.org -k %s" (string release.SemVer) (Environment.environVar "nugetkey")
    let result = exec "src/bin/Release" "nuget" args
    if (not result.OK) then failwithf "%A" result.Errors

    let args = sprintf "push Elmish.UrlParser.%s.nupkg -s nuget.org -k %s" (string release.SemVer) (Environment.environVar "nugetkey")
    let result = exec "netstandard/bin/Release" "nuget" args
    if (not result.OK) then failwithf "%A" result.Errors
)


// --------------------------------------------------------------------------------------
// Generate the documentation
Target.create "GenerateDocs" (fun _ ->
    let res = Shell.Exec("npm", "run docs:build")

    if res <> 0 then
        failwithf "Failed to generate docs"
)

Target.create "WatchDocs" (fun _ ->
    let res = Shell.Exec("npm", "run docs:watch")

    if res <> 0 then
        failwithf "Failed to watch docs: %d" res
)

// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "ReleaseDocs" (fun _ ->
    let res = Shell.Exec("npm", "run docs:publish")

    if res <> 0 then
        failwithf "Failed to publish docs: %d" res
)

Target.create "Publish" ignore

// Build order
"Clean"
    ==> "Meta"
    ==> "Restore"
    ==> "Build"
    ==> "Test"
    ==> "Package"
    ==> "PublishNuget"
    ==> "Publish"

// start build
Target.runOrDefault "Test"
