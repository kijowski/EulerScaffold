open System
open System.IO
open System.Collections.Generic

// --------------------------------
// init.fsx 
// This file is run the first time that you run build.sh/build.cmd
// It generates the build.fsx and generate.fsx files 
// --------------------------------

// --------------------------------
// helper functions brought from FAKE
// --------------------------------
let inline directoryInfo path = new DirectoryInfo(path)
let inline subDirectories (dir : DirectoryInfo) = dir.GetDirectories()
let filesInDirMatching pattern (dir : DirectoryInfo) = 
    if dir.Exists then dir.GetFiles pattern
    else [||]
let inline combinePaths path1 (path2 : string) = Path.Combine(path1, path2.TrimStart [| '\\'; '/' |])
let inline (@@) path1 path2 = combinePaths path1 path2

// special funtions
// many whom might be replaceable with FAKE functions

let failfUnlessExists f msg p = if not <| File.Exists f then failwithf msg p
let combine p1 p2 = Path.Combine(p2, p1)
let move p1 p2 = 
  if File.Exists p1 then
    printfn "moving %s to %s" p1 p2
    File.Move(p1, p2)
  elif Directory.Exists p1 then
    printfn "moving directory %s to %s" p1 p2
    Directory.Move(p1, p2)
  else
    failwithf "Could not move %s to %s" p1 p2
let localFile f = combine f __SOURCE_DIRECTORY__ 

let prompt (msg:string) = 
  Console.Write(msg)
  Console.ReadLine().Trim() 
  |> function | "" -> None | s -> Some s
  |> Option.map (fun s -> s.Replace ("\"","\\\""))
let promptFor friendlyName = 
  prompt (sprintf "%s: " friendlyName)
let rec promptForNoSpaces friendlyName =
  match promptFor friendlyName with
  | None -> None
  | Some s when not <| String.exists (fun c -> c = ' ') s -> Some s
  | _ -> Console.WriteLine("Sorry, spaces are disallowed"); promptForNoSpaces friendlyName

// User input
let border = "#####################################################"
let print msg = 
  printfn """
  %s
  %s
  %s
  """ border msg border

print """
# Project Euler Init Script
# Please answer a few questions and we will generate
# solution with script files for Project Euler problems and 
# build.fsx build script 
#
# NOTE: Aside from the Project Name, you may leave any 
# of these blank, but you will need to change the defaults 
# in the generated scripts.
#
"""

let vars = Dictionary<string,string option>()
vars.["##ProjectName##"] <- promptForNoSpaces "Project Name (used for solution/project files)"
vars.["##Summary##"]     <- promptFor "Summary (a short description)"
vars.["##Author##"]      <- promptFor "Author"

//Basic settings

let solutionTemplateName = "Euler.ProjectScaffold"
let projectTemplateName = "Euler.ProjectTemplate"
let oldProjectGuid = "7E90D6CE-A10B-4858-A5BC-41DF7250CBCA"
let projectGuid = Guid.NewGuid().ToString()

//Rename solution file
let templateSolutionFile = localFile (sprintf "%s.sln" solutionTemplateName)
failfUnlessExists templateSolutionFile "Cannot find solution file template %s"
            (templateSolutionFile |> Path.GetFullPath)

let projectName = 
  match vars.["##ProjectName##"] with
  | Some p -> p.Replace(" ", "")
  | None -> "ProjectEuler"
let solutionFile = localFile (projectName + ".sln")
move templateSolutionFile solutionFile

let dirWithProject = directoryInfo (__SOURCE_DIRECTORY__ @@ "src")
//Rename project file
dirWithProject 
|> subDirectories
|> Array.collect (fun d -> filesInDirMatching "*.?sproj" d)
|> Array.iter (fun f -> f.MoveTo(f.Directory.FullName @@ (f.Name.Replace(projectTemplateName, projectName))))
//Rename project directory
dirWithProject 
|> subDirectories
|> Array.iter (fun d -> d.MoveTo(dirWithProject.FullName @@ (d.Name.Replace(projectTemplateName, projectName))))
    

//Now that everything is renamed, we need to update the content of some files
let replace t r (lines:seq<string>) = 
  seq { 
    for s in lines do 
      if s.Contains(t) then yield s.Replace(t, r) 
      else yield s }

let replaceWithVarOrMsg t n lines = 
    replace t (vars.[t] |> function | None -> n | Some s -> s) lines

let overwrite file content = File.WriteAllLines(file, content |> Seq.toArray); file 

let replaceContent file = 
  File.ReadAllLines(file) |> Array.toSeq
  |> replace projectTemplateName projectName
  |> replace (oldProjectGuid.ToLowerInvariant()) (projectGuid.ToLowerInvariant())
  |> replace (oldProjectGuid.ToUpperInvariant()) (projectGuid.ToUpperInvariant())
  |> replace solutionTemplateName projectName
  |> replaceWithVarOrMsg "##Author##" "Author not set" 
  |> replaceWithVarOrMsg "##Summary##" ""
  |> overwrite file
  |> sprintf "%s updated"

let rec filesToReplace dir = seq {
  yield! Directory.GetFiles(dir, "*.?sproj")
  yield! Directory.GetFiles(dir, "*.fs")
  yield! Directory.GetFiles(dir, "*.cs")
  yield! Directory.GetFiles(dir, "*.xaml")
  yield! Directory.GetFiles(dir, "*.fsx")
  yield! Directory.EnumerateDirectories(dir) |> Seq.collect filesToReplace
}

[solutionFile] @ (dirWithProject.FullName |> filesToReplace |> List.ofSeq) 
|> List.map replaceContent
|> List.iter print

// Generate project

// Prompt user for generation parameters
let rec promptForNumber defNumber friendlyName =
  match promptFor friendlyName with
  | Some s when fst (Int32.TryParse(s)) -> Int32.Parse(s)
  | None -> defNumber
  | _ -> Console.WriteLine("Please enter an integer") ; promptForNumber defNumber friendlyName

print  """
# You can specify one of the following project structures:
# 1. One problem per file. All files in root project directory
# 2. One problem per file. Files grouped in solution folders (user specified folder size)
# 3. Multiple problem per file. All files in root project directory
# 4. Multiple problem per file. Files grouped in solution folders (user specified folder size)
"""

let problemsPerFile = promptForNumber 1 "How many problems per file (default 1)"
let filesPerDirectory = 
    match promptForNumber 25 "How many files per directory (default 25, set to 0 for all files in root directory)" with
    | 0 -> None
    | x -> Some x

// Load libraries for project manipulation and Json parsing
#r @"Microsoft.Build.dll"
#r @"packages\FSharp.Data\lib\net40\FSharp.Data.dll"
open Microsoft.Build.Evaluation
open FSharp.Data
open FSharp.Data.JsonExtensions

// Simple domain model
type EulerProblem = {
    Number : int
    Title : string
    Content : string
    Difficulty : string
}

let parseProblems (file:string) =
    [for problem in JsonValue.Load(file) do
        yield {Number=problem?Number.AsInteger()
               Title = problem?Title.AsString()
               Content = problem?Content.AsString()
               Difficulty=problem?Difficulty.AsString()
               }
    ]   

let rootProjectDir = dirWithProject.FullName @@ projectName
let rootDataDir = localFile "data" 
let project = new Project(rootProjectDir @@ (projectName + ".fsproj"))
let jsonFile = rootDataDir @@ "problems.json"
let templateFile = 
    if problemsPerFile = 1 then rootDataDir @@ "single.template"
    else rootDataDir @@ "multiple.template"
    

let applyTemplate problem =
    File.ReadAllLines(templateFile)
    |> replace "@Number" (problem.Number.ToString())
    |> replace "@Title" problem.Title
    |> replace "@Content" problem.Content
    |> replace "@Difficulty" problem.Difficulty
    |> replace "@NestLevel" (if filesPerDirectory.IsSome then @"..\" else @"")    
    |> Seq.reduce(fun line1 line2 -> line1 + Environment.NewLine + line2)
    
let fileName i =
    if problemsPerFile = 1 then sprintf "Problem%03i.fsx" (i+1)
    else sprintf "Problems%03i-%03i.fsx" (i * problemsPerFile + 1) ((i+1) * problemsPerFile) 

let folderName i =
    match filesPerDirectory with
        | Some fpd -> sprintf "%03i-%03i" (i * fpd * problemsPerFile + 1) ((i+1) * fpd * problemsPerFile) 
        | None -> ""


let addProblem folder (fileName, content) =
    let absFolderPath = rootProjectDir @@ folder
    if not (Directory.Exists(absFolderPath)) then Directory.CreateDirectory absFolderPath |> ignore            
    File.WriteAllText(absFolderPath @@ fileName, content)
    project.AddItem("None", folder @@ fileName) |> ignore
    printfn "Added: %s" (folder @@ fileName)

parseProblems jsonFile
|> List.sortBy(fun prob -> prob.Number)
|> Seq.mapi (fun i v -> i, v) // Add indices to the values (as first tuple element)
|> Seq.groupBy (fun (i, v) -> i/problemsPerFile) 
|> Seq.map (fun (i, v) -> v |> Seq.map snd) 
|> Seq.mapi(fun i problems -> fileName i, problems)
|> Seq.map(fun (fileName, problems) -> fileName,problems |> Seq.map(applyTemplate) |> Seq.reduce(fun line1 line2 -> line1 + Environment.NewLine + line2))
|> Seq.mapi (fun i v -> i, v) // Add indices to the values (as first tuple element)
|> Seq.groupBy (fun (i, v) -> i/defaultArg filesPerDirectory 1) 
|> Seq.map (fun (i, v) -> v |> Seq.map snd) 
|> Seq.mapi(fun i files -> folderName i, files)
|> Seq.iter(fun (folder, files) -> files |> Seq.iter(addProblem folder))

project.Save()

File.Delete "init.fsx"
File.Delete "build.cmd"

