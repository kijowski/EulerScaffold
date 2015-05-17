namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Euler.ProjectTemplate")>]
[<assembly: AssemblyProductAttribute("Euler.ProjectTemplate")>]
[<assembly: AssemblyDescriptionAttribute("##Summary##")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
