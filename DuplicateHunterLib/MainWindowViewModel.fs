namespace DuplicateHunter.ViewModel

open System.Collections.ObjectModel
open System
open System.IO
open System.Collections.Generic
open System.Collections.Concurrent
open System.ComponentModel
open System.Threading
open System.Windows.Forms

open DuplicateHunter.Model

open Prism.Mvvm
open Prism.Commands
open System.Windows.Threading


type CloneElement( parent: System.String, children: List<Item> ) =
    inherit BindableBase()
    
    let mutable parent1 = parent
    let mutable children1 = children 

    member x.Parent =
        parent1

    member x.Children =
        children1

        
type FolderViewModel() =
    inherit BindableBase()

    let mutable folderPath : string = ""

    member x.FolderPath
        with get() = folderPath
        and set value =
            folderPath <- value
            x.OnPropertyChanged (PropertyChangedEventArgs "FolderPath")


type MainWindowViewModel(progressContext: IProgressContext) =
    inherit BindableBase()

    let mutable folderPath = ""
    let mutable selectedFolder = ""

    let mutable foldersOnly = false
    let mutable isSizeSearch = true
    let mutable size = "1"
    let mutable isSizeInKB = false
    let mutable isSizeInMB = true
    let mutable isSizeInGB = false
    let mutable includeEmptyFolders = false
    let mutable includeEmptyFiles = false
    let mutable sizeInBytes = 0L
    let mutable isSearchParametersTabVisible = true
    let mutable isSearchResultsTabVisible = false
    let mutable isProcessing = false
    let syncContext = SynchronizationContext.Current

    let foldersToSearch = 
        new ObservableCollection<string>( 
            [ 
               
//            @"F:\Entertainment\My Videos\Songs";
//            @"F:\MyData Backup"
//            @"G:\Movies";
//            @"E:\Movies"
//            @"E:\Profiles\Netra\Downloads";
//            @"C:\Users\Karthik\Documents\Visual Studio 2010\Projects\Experiments\F#\CheckDuplicates\CheckDuplicates\bin1";
//            @"C:\Users\Karthik\Documents\Visual Studio 2010\Projects\Experiments\F#\CheckDuplicates\CheckDuplicates\bin2";
//            @"C:\Users\Karthik\Documents\Visual Studio 2010\Projects\Experiments\F#\CheckDuplicates\CheckDuplicates\bin3";
//            @"C:\Users\Karthik\Documents\Visual Studio 2010\Projects\Experiments\F#\CheckDuplicates\CheckDuplicates\bin4";
            ] 
        )

    let foldersToExclude =
        new ObservableCollection<string>( 
            [ ] 
        )

    let mutable nameMap = new Dictionary<string, List<Item>>()
    let mutable sizeMap = new Dictionary<int64, List<Item>>()

    //let mutable clones = new ConcurrentBag<CloneGroup>()
    let mutable clones = new ObservableCollection<CloneGroup>()
    let mutable processedFolders = new ObservableCollection<FolderItem>()

    let mutable selectedFolderInfo = ""

    let worker = new BackgroundWorker(WorkerSupportsCancellation=true, WorkerReportsProgress=true )
    
    let (~~) (arg:obj) =
        match arg with
        | :? System.String as str -> System.String.IsNullOrEmpty(str) = false
        | _ -> true

    member x.IsSearchParametersTabVisible
        with get() = isSearchResultsTabVisible
        and set value =
            isSearchResultsTabVisible <- value
            x.OnPropertyChanged (PropertyChangedEventArgs "IsSearchParametersTabVisible")
            //x.OnPropertyChanged "IsSearchResultsTabVisible"

    member x.IsSearchResultsTabVisible
        with get() = isSearchResultsTabVisible
        and set value =
            isSearchResultsTabVisible <- value
            x.OnPropertyChanged (PropertyChangedEventArgs "IsSearchResultsTabVisible")
            //x.OnPropertyChanged "IsSearchParametersTabVisible"

    member x.IsInProgress
        with get() = isProcessing
        and set value =
            isProcessing <- value
            x.OnPropertyChanged (PropertyChangedEventArgs "IsInProgress")
            x.OnPropertyChanged (PropertyChangedEventArgs "CancelCommand")

    member x.FoldersOnly
        with get() = foldersOnly
        and set value =
            foldersOnly <- value
            x.OnPropertyChanged (PropertyChangedEventArgs "FoldersOnly")

    member x.IsSizeSearch
        with get() = isSizeSearch
        and set value =
            isSizeSearch <- value
            x.OnPropertyChanged (PropertyChangedEventArgs "IsSizeSearch")

    member x.Size
        with get() = size
        and set value =
            size <- ( match String.IsNullOrWhiteSpace value with
                    | true -> "0"
                    | _ -> value
            )
            if x.IsSizeInKB then
                sizeInBytes <- (Convert.ToInt64 value) * 1024L
            elif x.IsSizeInMB then
                sizeInBytes <- (Convert.ToInt64 value) * 1024L * 1024L
            elif x.IsSizeInGB then
                sizeInBytes <- (Convert.ToInt64 value) * 1024L * 1024L * 1024L
            
            x.OnPropertyChanged (PropertyChangedEventArgs "Size")


    member x.IsSizeInKB
        with get() = isSizeInKB
        and set value =
            isSizeInKB <- value
            if value = true then
                sizeInBytes <- (Convert.ToInt64 x.Size) * 1024L
            x.OnPropertyChanged (PropertyChangedEventArgs "IsSizeInKB")

    member x.IsSizeInMB
        with get() = isSizeInMB
        and set value =
            isSizeInMB <- value
            if value = true then
                sizeInBytes <- (Convert.ToInt64 x.Size) * 1024L * 1024L
            x.OnPropertyChanged (PropertyChangedEventArgs "IsSizeInMB")

    member x.IsSizeInGB
        with get() = isSizeInGB
        and set value =
            isSizeInGB <- value
            if value = true then
                sizeInBytes <- (Convert.ToInt64 x.Size) * 1024L * 1024L * 1024L
            x.OnPropertyChanged (PropertyChangedEventArgs "IsSizeInGB")

    member x.IncludeEmptyFolders
        with get() = includeEmptyFolders
        and set value =
            includeEmptyFolders <- value
            x.OnPropertyChanged (PropertyChangedEventArgs "IncludeEmptyFolders")

    member x.IncludeEmptyFiles
        with get() = includeEmptyFiles
        and set value =
            includeEmptyFiles <- value
            x.OnPropertyChanged (PropertyChangedEventArgs "IncludeEmptyFiles")

    member x.FolderPath
        with get() = folderPath
        and set value =
            folderPath <- value
            x.OnPropertyChanged (PropertyChangedEventArgs "FolderPath")
            x.OnPropertyChanged (PropertyChangedEventArgs "AddFolderCommand")

    member x.SelectedFolderItem 
        with get() = selectedFolderInfo
        and set value =
            selectedFolderInfo <- value
            x.OnPropertyChanged (PropertyChangedEventArgs "SelectedFolderItem")
            x.OnPropertyChanged (PropertyChangedEventArgs "AddFolderToIncludeListCommand")
            x.OnPropertyChanged (PropertyChangedEventArgs "AddFolderToExcludeListCommand")

    member x.AddFolderCommand =
        new DelegateCommand( 
            (fun arg -> 
                foldersToSearch.Add ( folderPath )
                x.RaisePropertyChanged "HuntClonesCommand"
            ), 
            (fun arg-> 
                (System.String.IsNullOrEmpty(folderPath) = false) && Directory.Exists folderPath
            ) 
        ) 

    member x.RaisePropertyChanged (propertyName: string) =
        x.OnPropertyChanged (PropertyChangedEventArgs propertyName)

    member x.FoldersToSearch
        with get() = foldersToSearch

    member x.FoldersToExclude
        with get() = foldersToExclude
    
    member x.SelectedFolder 
        with get() = selectedFolder
        and set value = 
            selectedFolder <- value
            x.OnPropertyChanged (PropertyChangedEventArgs "SelectedFolder")
            x.OnPropertyChanged (PropertyChangedEventArgs "RemoveSelectedFolderCommand")

    member x.AddFolderToIncludeListCommand =
        new DelegateCommand(
            (fun arg ->
                if (System.String.IsNullOrEmpty(x.SelectedFolderItem) = false) then
                    foldersToSearch.Add( x.SelectedFolderItem ) |> ignore
                    x.RaisePropertyChanged "HuntClonesCommand"
            ),
            (fun arg -> ~~ x.SelectedFolderItem)
        )

    member x.AddFolderToExcludeListCommand =
        new DelegateCommand(
            (fun arg ->
                if (System.String.IsNullOrEmpty(x.SelectedFolderItem) = false) then
                    foldersToExclude.Add( x.SelectedFolderItem ) |> ignore
                    x.RaisePropertyChanged "HuntClonesCommand"
            ),
            (fun arg -> ~~ x.SelectedFolderItem)
        )
        
    member x.RemoveFolderFromIncludeListCommand = 
        new DelegateCommand<Object>( 
            (fun arg ->
                if (~~ arg) then
                    foldersToSearch.Remove(arg.ToString()) |> ignore
                    x.RaisePropertyChanged "HuntClonesCommand"
                    if foldersToSearch.Count = 0 then
                        x.SelectedFolder <- ""
            ),
            (fun arg -> ~~ arg)
        )

    member x.RemoveFolderFromExcludeListCommand = 
        new DelegateCommand<Object>( 
            (fun arg ->
                if (~~ arg) then
                    foldersToExclude.Remove(arg.ToString()) |> ignore
                    x.RaisePropertyChanged "HuntClonesCommand"
            ),
            (fun arg -> ~~ arg)
        )



    member x.RemoveSelectedFolderCommand = 
        new DelegateCommand( 
            (fun arg ->
                if (~~ x.SelectedFolder) then
                    foldersToSearch.Remove( x.SelectedFolder ) |> ignore
                    x.RaisePropertyChanged "HuntClonesCommand"
                    if foldersToSearch.Count = 0 then
                        x.SelectedFolder <- ""
            ),
            (fun arg -> ~~ x.SelectedFolder)
        )

    member x.HuntClonesCommand =
        new DelegateCommand(
            (fun arg ->
                clones.Clear()
                x.RaisePropertyChanged "Clones"
                x.IsSearchResultsTabVisible <- true
                

                let startTime = DateTime.Now

//                let parameters = new SearchParameters( foldersToSearch, foldersToExclude, clones, FoldersOnly=foldersOnly, IsSizeSearch=x.IsSizeSearch, SizeInBytes=sizeInBytes )
//                let worker = new CloneHunterWorker(parameters)

                worker = new BackgroundWorker(WorkerSupportsCancellation=true, WorkerReportsProgress=true ) |> ignore

                worker.DoWork.Add( 
                    fun args ->
                        let folders = DuplicateHunterLib.HuntClones progressContext syncContext x.FoldersToSearch x.FoldersToExclude x.FoldersOnly x.IsSizeSearch sizeInBytes false false 
                        folders 
                        |> Seq.iter (fun pf -> 
                            
                            Dispatcher.CurrentDispatcher.Invoke( DispatcherPriority.Normal,
                                Action(fun () -> 
                                    processedFolders.Clear()
                                    processedFolders.Add(pf)
                                )) |> ignore 
                                ) 
                    )


                worker.RunWorkerAsync()

                //CompareLib.HuntClones foldersToSearch foldersToExclude x.FoldersOnly x.IsSizeSearch sizeInBytes x.IncludeEmptyFolders x.IncludeEmptyFiles clones 
                x.IsInProgress <- true

                worker.RunWorkerCompleted.Add( fun args ->
                        x.IsInProgress <- false
                        progressContext.Canceled <- false
                    )

                let timeTaken = DateTime.Now - startTime
//                              
//
//                let huntClonesAsync = 
//                    async 
//                    {
//                        do CompareLib.HuntClones foldersToSearch clones 
////                    }
//                
//                Async.Start huntClonesAsync
//                foldersToSearch 
//                |> Seq.iter (fun folder -> 
//                                CompareLib.ProcessFolder folder nameMap sizeMap |> ignore )

                x.RaisePropertyChanged "ClonesBySize"
                
                ()
            ),
            (fun arg -> x.IsInProgress = false && foldersToSearch.Count > 0)
        )

    member x.Worker = worker

    member x.CancelCommand =
        new DelegateCommand(
            (fun arg ->
                x.Worker.CancelAsync()
                progressContext.Canceled <- true
                x.IsInProgress <- false
            ),
            (fun arg -> x.IsInProgress)
        )

    member x.Clones 
        with get() =
            clones

    member x.ProcessedFolders
        with get() =
            processedFolders

    member x.ClonesByName
        with get() = nameMap

    member x.Refresh() =
        let itemsToAdd = 
            clones
            |> Seq.filter (fun clone -> clone.CloneItems |> Seq.exists(fun item -> item.IsFile || item.IsFolder))
            |> Seq.toList

        clones.Clear()

        itemsToAdd
        |> Seq.iter (fun item -> clones.Add(item))

        x.OnPropertyChanged (PropertyChangedEventArgs "Clones")


    
