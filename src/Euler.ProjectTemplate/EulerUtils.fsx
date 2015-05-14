let sendAnswer problem answer =
    System.Windows.Forms.Clipboard.SetText(answer)
    System.Diagnostics.Process.Start(sprintf "https://projecteuler.net/problem=%d" problem)
