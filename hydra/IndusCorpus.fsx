// Generated from indus_corpus.db by SqlHydra.Sqlite v4.0.0-beta.3
// then converted to .fsx for #load from decoder scripts.
// Regenerate: cd hydra && sqlhydra sqlite -t sqlhydra-corpus.toml && convert IndusCorpus.fs -> .fsx

#r "nuget: SqlHydra.Query, 4.0.0-beta.3"
#r "nuget: Microsoft.Data.Sqlite, 9.0.4"

open SqlHydra
open SqlHydra.Query

module IndusCorpus =

    module Version =
        let cli = System.Version(4, 0, 0)
        let ns = "IndusCorpus"
        SqlHydra.Query.VersionCheck.assertIsCompatible cli ns

    module main =

        [<CLIMutable>]
        type base_sign =
            { source_code: string
              sign_id: string
              label: Option<string>
              variants: Option<int64>
              composites: Option<int64>
              total: Option<int64> }

        [<CLIMutable>]
        type cisi_inscription =
            { id: string
              description: Option<string>
              signs: string
              sign_count: int64
              source_code: string }

        [<CLIMutable>]
        type inscription =
            { id: string
              source_code: string
              sign_sequence: string
              sign_count: int64 }

        [<CLIMutable>]
        type morphological_parallel =
            { id: int64
              sign_a_src: string
              sign_a_id: string
              sign_b_src: string
              sign_b_id: string
              confidence: Option<string>
              note: Option<string> }

        [<CLIMutable>]
        type radiometric_measurement =
            { meas_id: int64
              sample_id: int64
              institution: string
              method: string
              radiometric_bp: Option<int64>
              error_bp: Option<int64>
              cal_bce_lo: Option<int64>
              cal_bce_hi: Option<int64>
              cal_bce_mid: Option<int64>
              probability: Option<double>
              source_table: Option<string>
              notes: Option<string> }

        [<CLIMutable>]
        type radiometric_sample =
            { sample_id: int64
              site_id: int64
              sample_no: string
              context: Option<string>
              depth_cm: Option<int64>
              material: Option<string>
              iron_context: Option<int64> }

        [<CLIMutable>]
        type radiometric_site =
            { site_id: int64
              site_name: string
              district: string
              state: string
              latitude: Option<double>
              longitude: Option<double>
              site_type: Option<string>
              river: Option<string> }

        [<CLIMutable>]
        type sign_concordance =
            { parpola_id: string
              description: Option<string>
              mahadevan_ids: Option<string>
              wells_ids: Option<string> }

        [<CLIMutable>]
        type sign_form =
            { source_code: string
              form_id: string
              parent_sign_id: string
              form_type: string
              label: Option<string> }

        [<CLIMutable>]
        type sign_occurrence =
            { id: int64
              artefact_id: string
              source_code: string
              sign_id: string
              position: int64 }

        [<CLIMutable>]
        type site =
            { id: string
              name: string
              source_code: string
              region: Option<string>
              period: Option<string> }

        [<CLIMutable>]
        type source =
            { code: string
              name: string
              year: Option<int64>
              note: Option<string> }

        [<CLIMutable>]
        type tamil_sentence =
            { id: int64
              original_tamil: string
              logosyllabic: Option<string>
              sign_count: Option<int64> }

        [<CLIMutable>]
        type treebank_token =
            { id: int64
              sentence_id: int64
              position: int64
              form: string
              lemma: string
              pos: string
              pos_detail: Option<string>
              features: Option<string>
              head: Option<int64>
              dep_rel: Option<string> }

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
