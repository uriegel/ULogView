﻿module LogServer

open System.IO
open System.Threading

open FSharpTools
open Restriction
open Session
open ULogViewServer

type LogSession = {
    Send: (obj->unit)
    Items: LineItem[]
    FilteredItems: LineItem[] option
    Restriction: Restriction option
}

let mutable private sessionIdGenerator = 0
let mutable logSessions = Map.empty<string, LogSession>

let createSession id send =
    logSessions <- logSessions.Add (id, { Send = send; Items = [||]; Restriction = None; FilteredItems = None })

let createSessionId () = string (Interlocked.Increment &sessionIdGenerator)

let private onSocketSession (session: Types.Session) =
    let onReceive (payload: Stream) = ()
    let id = createSessionId ()
    let onClose () = logSessions <- logSessions.Remove id 
    let sendBytes = session.Start onReceive onClose
    let sendObject = Json.serializeToBuffer >> sendBytes
    createSession id sendObject
    
type Command = {
    Cmd: string
    RequestId: string
    Count: int64
}

let updateSession id getUpdate = 
    let changeItem maybeItem = 
        match maybeItem with
        | Some item -> Some (getUpdate item) //  { item with Restriction = None }
        | None -> None
    logSessions <- logSessions |> Map.change id changeItem

let changeItems sessionId = 
    let session = logSessions.Item sessionId
    let count = 
        match session.FilteredItems with
        | Some items -> items.Length
        | None -> session.Items.Length
    session.Send ({ Id = sessionId; LineCount = count } :> obj) 

let request (requestSession: RequestSession) =

    async {
        let request = requestSession.Query.Value
        match requestSession.Query.Value.Request with
        | "getitems" ->
            match request.Query "id", request.Query "req", request.Query "start", request.Query "end" with
            | Some id, Some req, Some startIndex, Some endIndex ->
                let session = logSessions.Item id
                let items = 
                    match session.FilteredItems with
                    | Some items -> items
                    | None -> session.Items
                let result = items.[int startIndex..int endIndex] 

                let getLogItem item = 
                    let highlightedText =
                        match session.Restriction with 
                        | Some restriction -> getHighlightedParts restriction.Keywords item.Text |> List.toArray
                        | None -> null                        
                    {
                        HighlightedText = highlightedText 
                        Text = item.Text
                        Index = item.Index
                        FileIndex = item.FileIndex
                    }

                let result = 
                    {
                        Request = int req
                        Items = result |> Array.map getLogItem
                    }
                do! requestSession.AsyncSendJson (result :> obj)
                return true
            | _ -> return false
        | "setrestrictions" ->
            match request.Query "id", request.Query "restriction" with
            | Some id, Some restriction when restriction.Length > 0 -> 
                let restriction = Restriction.getRestriction restriction
                updateSession id (fun item -> { item with Restriction = Some restriction })
                do! requestSession.AsyncSendJson ({||} :> obj)
                return true
            | Some id, Some restriction when restriction.Length = 0 -> 
                updateSession id (fun item -> { item with Restriction = None })
                do! requestSession.AsyncSendJson ({||} :> obj)
                return true
            | _ -> return false
        | "toggleview" ->
            match request.Query "id" with
            | Some id -> 
                let session = logSessions.Item id
                let isFolded = 
                    match session.Restriction, session.FilteredItems with
                    | Some _, Some filtered -> 
                        updateSession id (fun item -> { item with FilteredItems = None })
                        false
                    | Some restriction, None ->
                        let filter lineItem = 
                            if restriction.Restrictions |> filterRestriction lineItem.Text then Some lineItem else None
                        let adaptIndex index (lineItem: LineItem) = { lineItem with Index = index }
                        let filteredItems = session.Items |> Array.Parallel.choose filter |> Array.mapi adaptIndex
                        updateSession id (fun item -> { item with FilteredItems = Some filteredItems })
                        true
                    | None, _ ->
                        false
                changeItems id
                do! requestSession.AsyncSendJson ({| IsFolded = isFolded |} :> obj)
                return true
            | _ -> return false
        | _ -> return false
    }

let private configuration = Configuration.create {
    Configuration.createEmpty() with 
        Port = 9865
        AllowOrigins = Some [| "http://localhost:3000"; |]
        Requests = [ 
            Websocket.useWebsocket "/websocketurl" onSocketSession
            request
        ]
}
let private server = Server.create configuration 

let start () = server.start ()    

let indexFile logFile =
    // TODO Send Loading...
    let lines = LogFile.readLog logFile false
    logSessions <- logSessions |> Map.map (fun k item  -> { item with Items = lines })
    logSessions |> Map.iter (fun id _ -> changeItems id)
    
// TODO: ProgressView when loading file
// TODO ProgressView when toggleing
// TODO When restricted: set current index on first item after current item
// TODO When toggle: set current index
    


