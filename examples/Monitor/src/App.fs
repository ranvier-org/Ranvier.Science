/// The page. In this version it is a placeholder: one bound text and one chart scene with one mark.
module SequentialMonitor.App

open Ranvier
open Ranvier.D5
open Ranvier.D5.View
open Ranvier.View
open Ranvier.View.Dsl

/// The placeholder page's signals.
type State =
    { Text: Signal<string>
      Scene: Signal<Scene> }

let private setup (_: Setup) (props: Props) : State =
    let state =
        { Text = Api.createSignal "probe page"
          Scene = Api.createSignal (Scene.create 320.0 80.0 "probe" [ Mark.text "probe/label" (Point.create 16.0 40.0) "probe" ]) }

    props.Session.Page <- box state
    state

let app: Component<Props> =
    Component.template
        setup
        (div () {
            div (div.id "probe") { fun (s: State) -> s.Text.Value }
            render Renderer.passive (fun (s: State) -> fun () -> s.Scene.Value)
        })
