
#r "../packages/Suave/lib/net40/Suave.dll"

open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful

let rec getQueryParam (queryParams: (string * string option) list) (key:string) : string option =
    match queryParams with
        | []                        -> None
        | (headKey, headValue) :: tail -> match headKey with
                                            | k when k = key -> headValue
                                            | _   -> getQueryParam tail key
let helloHandler = fun (request : HttpRequest) ->
    OK <| match getQueryParam request.query "name" with
            | Some value -> sprintf "Hello %s" <| value
            | None       -> "Hello World"