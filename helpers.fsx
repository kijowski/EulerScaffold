#r @"packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#r @"Microsoft.Build.dll"
open FSharp.Data
open System
open System.Text.RegularExpressions
open Microsoft.Build.Evaluation
open System.IO
open System.Net

// helpers brought from Project Scaffolding
let inline combinePaths path1 (path2 : string) = Path.Combine(path1, path2.TrimStart [| '\\'; '/' |])
let inline (@@) path1 path2 = combinePaths path1 path2
let replace t r (lines:seq<string>) =
  seq {
    for s in lines do
      if s.Contains(t) then yield s.Replace(t, r)
      else yield s }
       
// domain model for project euler problem definitions
type EulerProblem = {
  Number : int
  Title : string
  Content : string
  Difficulty : string
  Raw : string
}

let (|SimpleNode|ComplexNode|) (node:HtmlNode) =
  if node.Elements().IsEmpty 
  then SimpleNode (node.Name(), node.InnerText())
  else ComplexNode (node.Name(), node.Elements())


let downloadProblem imagesPath n =
  let username = "username";
  let password = "******";

  let cc = CookieContainer()
  Http.Request(@"https://projecteuler.net/sign_in",
   body = FormValues ["username", username
                      "password", password
                      "remember_me", "1"
                      "sign_in","Sign+In"
                     ], cookieContainer=cc) |> ignore

  let rec parseProblem (node:HtmlNode) = 
    match node with
      | SimpleNode("img",_) -> if node.TryGetAttribute("src").IsSome then
                                 let file = @"https://projecteuler.net/" + node.Attribute("src").Value()
                                 use client = new WebClient()
                                 client.DownloadFile(file, imagesPath @@ Path.GetFileName(file))
                                 sprintf "![alt text](%s)" (node.Attribute("src").Value())
                               else ""
      | SimpleNode("b",text) -> "**" + text + "**"
      | SimpleNode(name,text) -> text
      | ComplexNode(name,elems) -> elems |> List.map parseProblem |> List.reduce(+)
  let getDifficulty (node:HtmlNode) = 
    printfn "%s" (node.InnerText())
    let reg = Regex.Match(node.InnerText(), ".*Difficulty rating\: (?<diff>\d*).*")
    if reg.Success then reg.Groups.["diff"].Value else "unknown"
  let problemPage = Http.RequestString(sprintf "https://projecteuler.net/problem=%d" n, cookieContainer=cc)
  match problemPage.Contains("Problem not accessible") with
    | false ->  
              let problemPageParsed = HtmlDocument.Parse(problemPage)    
              let title = problemPageParsed.Descendants ["h2"] |> Seq.head
              let problemInfo = problemPageParsed.Descendants(fun x -> x.HasId("problem_info")) |> Seq.head
              let problemContent = problemPageParsed.Descendants(fun x -> x.HasClass("problem_content")) |> Seq.head
              Some( {
                     Number = n
                     Title = title.InnerText()
                     Content = parseProblem problemContent
                     Difficulty = getDifficulty problemInfo
                     Raw = problemPage
                    })
    | true -> None

let applyTemplate templateFile problem =
  File.ReadAllLines(templateFile)
  |> replace "@Number" (problem.Number.ToString())
  |> replace "@Title" problem.Title
  |> replace "@Content" problem.Content
  |> replace "@Difficulty" problem.Difficulty
  |> Seq.reduce(fun line1 line2 -> line1 + Environment.NewLine + line2)

let joinProblems preambleFile templateFile nestLevel problems =
  let processedPreamble = 
    File.ReadAllLines(preambleFile)
    |> replace "@NestLevel" nestLevel
    |> Seq.reduce(fun line1 line2 -> line1 + Environment.NewLine + line2)
  problems 
  |> Seq.map(applyTemplate templateFile) 
  |> Seq.fold(fun line1 line2 -> line1 + Environment.NewLine + line2) processedPreamble

let addProblems (projectFile:string) folder files =
  let absFolderPath = Path.GetDirectoryName(projectFile) @@ folder
  if not (Directory.Exists(absFolderPath)) then Directory.CreateDirectory absFolderPath |> ignore
  files
  |> Seq.map(fun (fileName, content) -> 
                  File.WriteAllText(absFolderPath @@ fileName, content) 
                  printfn "Added: %s" (folder @@ fileName)
                  sprintf @"<None Include=""%s"" />" (folder @@ fileName)
            )
  |> Seq.reduce(fun st1 st2 -> sprintf "%s\n%s" st1 st2)

let addAllProblems (projectFile:string) (contents:(string*(string*string) seq) seq) =
  let files =
    contents
    |> Seq.map(fun (folder,files) -> addProblems projectFile folder files)
    |> Seq.reduce(fun st1 st2 -> sprintf "%s\n%s" st1 st2)
  let projectContent = File.ReadAllText(projectFile)
  File.WriteAllText(projectFile, projectContent.Replace("@Include", files))
