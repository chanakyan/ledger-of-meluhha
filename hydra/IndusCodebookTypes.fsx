// SqlHydra-generated types for indus_codebook.db (7 tables)
// This file has NO #r directives — the loading script must provide them.
// Regenerate: cd hydra && sqlhydra sqlite

open SqlHydra
open SqlHydra.Query

module IndusCodebook =

    module main =

        [<CLIMutable>]
        type commodity =
            { code: Option<string>
              label: string
              description: Option<string>
              corpus_note: Option<string> }

        [<CLIMutable>]
        type merchant_mark =
            { code: Option<string>
              label: string
              note: Option<string> }

        [<CLIMutable>]
        type positional_rule =
            { position: Option<string>
              default_role: string }

        [<CLIMutable>]
        type quantity_code = { sign_id: int64; multiplier: int64 }

        [<CLIMutable>]
        type route =
            { code: Option<string>
              label: string
              destination: Option<string> }

        [<CLIMutable>]
        type sign_role =
            { sign_id: int64
              role: string
              ref_code: Option<string>
              ref_multiplier: Option<int64> }

        [<CLIMutable>]
        type weight_tier =
            { multiplier: int64
              grams: double
              series: string
              application: Option<string> }

    type QueryContextFactory =
        { OpenContext: unit -> QueryContext
          OpenContextAsync: unit -> System.Threading.Tasks.Task<QueryContext> }

        interface IQueryContextFactory with
            member this.OpenContextAsync() = this.OpenContextAsync()

        static member Create(connectionString: string, ?sqlLogger) =
            let emitter = SqlHydra.Query.SqliteEmitter()

            let createConn () : System.Data.Common.DbConnection =
                new Microsoft.Data.Sqlite.SqliteConnection(connectionString)

            let openContext () =
                let conn = createConn ()
                conn.Open()
                let ctx = new QueryContext(conn, emitter)
                sqlLogger |> Option.iter (fun logger -> ctx.Logger <- logger)
                ctx

            let openContextAsync () =
                task {
                    let conn = createConn ()
                    do! conn.OpenAsync()
                    let ctx = new QueryContext(conn, emitter)
                    sqlLogger |> Option.iter (fun logger -> ctx.Logger <- logger)
                    return ctx
                }

            { OpenContext = openContext
              OpenContextAsync = openContextAsync }
