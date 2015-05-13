#r @"packages\FSharp.Data.2.2.0\lib\net40\FSharp.Data.dll"
#r @"Microsoft.Build.dll"
open FSharp.Data
open System
open System.Text.RegularExpressions
open Microsoft.Build.Evaluation
open System.IO
open System.Collections.Generic
 
// domain model for project euler problem definitions
type EulerProblem = {
    Number : int
    Title : string
    Content : string
}
 
// helper functions
let regexRep (patt:string) (repl:string) input =
    Regex.Replace(input, patt, repl)
 
let processTemplate template problem =
    template
    |> regexRep "@Number" (problem.Number.ToString())
    |> regexRep "@Title" problem.Title
    |> regexRep "@Content" problem.Content
 
let downloadProblem n =
    let rec simpleParse (node:HtmlNode) =
        match node.Elements() |> Seq.isEmpty with
            | true -> node.InnerText()
            | false -> node.Elements() |> Seq.map simpleParse |> Seq.reduce(+)
 
    let problemPage = Http.RequestString(sprintf "https://projecteuler.net/problem=%d" n)
    match problemPage.Contains("Problem not accessible") with
        | false ->  
                let problemPage = HtmlDocument.Parse(problemPage)    
                let title = problemPage.Descendants ["h2"] |> Seq.head
                let problemInfo = problemPage.Descendants(fun x -> x.HasId("problem_info")) |> Seq.head
                let problemContent = problemPage.Descendants(fun x -> x.HasClass("problem_content")) |> Seq.head
                Some( {
                        Number = n
                        Title = title.InnerText()
                        Content = simpleParse problemContent
                      })
        | true -> None
    
let addProblem templateProcessor (project:Project) problemGroup eulerProblem =
    let folder = sprintf "D:\Source\Playground\ProjectEuler\ProjectEuler\%d" problemGroup
    if not (Directory.Exists(folder)) then Directory.CreateDirectory folder |> ignore
    let fileName = Path.Combine(sprintf "%d" problemGroup, sprintf "Problem%d.fsx" eulerProblem.Number)
    printfn "%s" fileName
    File.WriteAllText(Path.Combine("D:\Source\Playground\ProjectEuler\ProjectEuler", fileName), eulerProblem |> templateProcessor)
    project.AddItem("None", fileName) |> ignore
 
let downloadAll template project =
    Seq.initInfinite (fun x->x+1) 
    |> Seq.map(fun n -> downloadProblem n) 
    |> Seq.takeWhile(fun problem -> problem.IsSome) 
    |> Seq.map Option.get
    |> Seq.groupBy(fun problem -> problem.Number/26)
    |> Seq.iter(fun (group, problems) -> problems |> Seq.iter (addProblem template project group))
 
    
let projectFilePath = @"D:\Source\Playground\ProjectEuler\ProjectEuler\ProjectEuler.fsproj"
let templateContent = File.ReadAllText(@"D:\Source\Playground\ProjectEuler\ProjectEuler\Problem.template")
let definitions = Dictionary<string,string>()
definitions.Clear()
definitions.Add("MSBuildExtensionsPath32",@"C:\Program Files (x86)\MSBuild")
definitions.Add("VisualStudioVersion",@"12.0")
definitions.Add("FSharpTargetsPath",@"C:\Program Files (x86)\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets")
 
let project = Project(projectFilePath,definitions,"12.0")
 
downloadAll (processTemplate templateContent) project
project.Save()
