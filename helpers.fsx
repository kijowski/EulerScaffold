#r @"packages\FSharp.Data.2.2.0\lib\net40\FSharp.Data.dll"
#r @"Microsoft.Build.dll"
open FSharp.Data
open System
open System.Text.RegularExpressions
open Microsoft.Build.Evaluation
open System.IO
open System.Collections.Generic
open System.Net
 
// domain model for project euler problem definitions
type EulerProblem = {
  Number : int
  Title : string
  Content : string
  Difficulty : string
  Raw : string
}
 
// helper functions
let regexRep (patt:string) (repl:string) input =
  Regex.Replace(input, patt, repl)
 
let processTemplate template problem =
  template
  |> regexRep "@Number" (problem.Number.ToString())
  |> regexRep "@Title" problem.Title
  |> regexRep "@Content" problem.Content
 
let username = "username";
let password = "******";

let cc = CookieContainer()
Http.Request(@"https://projecteuler.net/sign_in",
  body = FormValues ["username", username
                   "password", password
                     "remember_me", "1"
                     "sign_in","Sign+In"
                     ], cookieContainer=cc) |> ignore

let (|SimpleNode|ComplexNode|) (node:HtmlNode) =
  if node.Elements().IsEmpty 
  then SimpleNode (node.Name(), node.InnerText())
  else ComplexNode (node.Name(), node.Elements())


let downloadProblem n =
  let rec parseProblem (node:HtmlNode) = 
    match node with
      | SimpleNode("img",_) -> if node.TryGetAttribute("src").IsSome then
                                 let file = @"https://projecteuler.net/" + node.Attribute("src").Value()
                                 use client = new WebClient()
                                 client.DownloadFile(file, @"C:\Users\michal\Downloads\ProjectScaffold-master\ProjectScaffold-master\src\ProjectEuler\" + Path.GetFileName(file))
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

downloadProblem 405
