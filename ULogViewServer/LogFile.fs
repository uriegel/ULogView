﻿module LogFile

open System.Globalization
open System.IO
open System.Text

open ULogViewServer

let readLog filePath isUtf8 = 
    Encoding.RegisterProvider CodePagesEncodingProvider.Instance 
    let encoding = if isUtf8 then Encoding.UTF8 else Encoding.GetEncoding (CultureInfo.CurrentCulture.TextInfo.ANSICodePage)
    use stream = new FileStream (filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite) 
    use reader = new StreamReader (stream, encoding)

    let rec readLines () = 
        seq {
            let textline = reader.ReadLine ()
            match isNull textline with
            | false -> 
                    yield textline
                    yield! readLines ()
            | true -> ()
        }
        
    readLines ()
    |> Seq.mapi (fun i n -> { Text = n; Index = i; FileIndex = i; HighlightedText = null })
    |> Seq.toArray
        
        