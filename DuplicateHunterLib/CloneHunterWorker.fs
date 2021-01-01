namespace DuplicateHunter.ViewModel

open System.Collections.ObjectModel
open System.ComponentModel
open System.Windows.Forms
open Microsoft.FSharp.Core

open System.Threading
open GalaSoft.MvvmLight
open DuplicateHunter.Model

type SearchParameters (foldersToHunt : seq<System.String>, foldersToExclude : seq<System.String>, cloneGroups : ObservableCollection<CloneGroup>) =
    inherit ViewModelBase()
     
    let mutable foldersOnly = true 
    let mutable isSizeSearch = false
    let mutable sizeInBytes = 0L 
//    let mutable cloneGroups = new ObservableCollection<CompareLib.CloneGroup>()

    member x.FoldersToHunt = foldersToHunt

    member x.FoldersToExclude = foldersToExclude

    member x.FoldersOnly 
        with get() = foldersOnly
        and set value = 
            foldersOnly <- value

    member x.IsSizeSearch 
        with get() = isSizeSearch
        and set value = 
            isSizeSearch <- value

    member x.SizeInBytes 
        with get() = sizeInBytes
        and set value = 
            sizeInBytes <- value

    member x.Clones 
        with get() = cloneGroups


type CloneHunterWorker
    (parameters : SearchParameters) =
    
    let worker = new BackgroundWorker(WorkerReportsProgress = true, WorkerSupportsCancellation = true)
    let syncContext = SynchronizationContext.Current

    do worker.DoWork.Add( fun args ->
        DuplicateHunterLib.HuntClones (args.Argument:?>IProgressContext) syncContext parameters.FoldersToHunt parameters.FoldersToExclude parameters.FoldersOnly parameters.IsSizeSearch parameters.SizeInBytes false false  
        |> ignore
        )

    do worker.RunWorkerCompleted.Add( fun args ->
            ()
        )

    let completed = new Event<_>()

    member x.Completed =
        completed.Trigger(null)

    member x.RunWorkerAsync(arg:IProgressContext) =
        worker.RunWorkerAsync(arg)

    member x.CancelAsync =
        worker.CancelAsync


