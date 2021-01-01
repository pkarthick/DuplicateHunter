namespace DuplicateHunter

open System
open System.IO

open System.Collections.Generic
open System.Collections.Concurrent
open System.ComponentModel

module Model =
    open Prism.Mvvm
    open Prism.Commands

    type ItemType =
        | Folder = 0
        | File = 1
        | Set = 3

    type DeltaType = 
        | UnAssigned = 0
        | LeftOnly = 1
        | RightOnly = 2
        | Different = 3
        | Identical = 4

    type DeltaItem = { FileName:string; DeltaType: DeltaType; LeftFolder:string; RightFolder:string; ItemType: ItemType; DeltaItems: DeltaItem list; }

    let toReadableSize (size:int64) =
        if size > 1000000000000L then
            (Convert.ToSingle(size)/1000000000000.0F).ToString("0.00 TB")
        elif size > 1000000000L then
            (Convert.ToSingle(size)/1000000000.0F).ToString("0.00 GB")
        elif size > 1000000L then
            (Convert.ToSingle(size)/1000000.0F).ToString("0.00 MB")
        elif size > 1000L then
            (Convert.ToSingle(size)/1000.0F).ToString() + " KB"
        elif size >= 0L then
            size.ToString() + " bytes"
        else
            raise <| InvalidDataException()

    type FileItem( fileInfo: FileInfo ) = 

        let mutable isClone = false
        
        member f.Name = fileInfo.Name
        member f.Size with get() = fileInfo.Length
    
        member f.IsClone 
            with get() = isClone
            and set value =
                isClone <- value

        member f.DisplaySize =
            toReadableSize fileInfo.Length

        member f.Path with get() = fileInfo.DirectoryName
        member f.FullName with get() = fileInfo.FullName
        
        override f.Equals(o : obj) = 
            match o with
            |  :? FileItem as fi -> fi.FullName = fileInfo.FullName && fi.Size = fileInfo.Length
            | _ -> false

        override f.GetHashCode() =
            fileInfo.GetHashCode()

    type FolderItem = { Name: string; IsSelected: bool; FullName: string; Size: int64; DisplaySize:string; Files : FileItem list; SubFolders : FolderItem list; mutable IsClone: bool; }

     type Item =
        | File of FileItem
        | Folder of FolderItem 

   
    type CloneItem (itemPath:System.String, itemSize:int64, cg:CloneGroup) =

        inherit BindableBase()
      
        let mutable groupKey = Guid.Empty

        let isFile = File.Exists(itemPath)
        let isFolder = Directory.Exists(itemPath)

        member x.Name with get() = Path.GetFileName(itemPath)

        member x.Size =
            itemSize

        member x.DisplaySize =
            toReadableSize itemSize

        member x.Path =
            itemPath

        member x.ParentFolder with get() = Path.GetDirectoryName(itemPath)

        member x.IsFile with get() = isFile

        member x.IsFolder with get() = isFolder

        member x.GroupKey 
            with get() = groupKey
            and set value =
                groupKey <- value
                x.OnPropertyChanged (PropertyChangedEventArgs "GroupKey")


    and CloneGroup() =
        inherit BindableBase()

        let mutable items = new List<CloneItem>()

        member x.Key = Guid.NewGuid()

        member x.CloneItems = 
             items

        member x.HasClones = items.Count > 1 

        member x.OpenWinmergeCommand =
            new DelegateCommand( 
                (fun arg -> 
                    arg |> ignore
                ) 
            ) 

        member x.AddItem(itemPath:string, itemSize: int64) =
            items.Add(new CloneItem(itemPath, itemSize, x))

        member x.GroupName = 
            let namesOfItems =
                items 
                |> Seq.map (fun item -> item.Name)            
                |> Seq.distinct 
                |> Seq.toList
        
            let groupName =

                match namesOfItems.Length with
                | 1  -> 
                        namesOfItems |> Seq.item 0
                | 2 | 3 ->
                        System.String.Join ( ", ", namesOfItems) 
                | _ ->
                        System.String.Join ( ", ", Seq.take 3 namesOfItems) + "..." 

            groupName + " (" + items.Count.ToString() + ")"
        

    let rec getFolderSize (cancel: bool) (dirInfo:DirectoryInfo) (folderSizeMap:ConcurrentDictionary<string, int64>) = 

        try
            match cancel with
            | true -> 0L
            | false ->
                match folderSizeMap.ContainsKey(dirInfo.FullName) with 
                | true -> folderSizeMap.[dirInfo.FullName]
                | false ->
                    let allFilesSize = 
                        dirInfo.GetFiles()
                        |> Seq.sumBy (fun file -> file.Length)

                    let allSubFolderSize = 
                        dirInfo.GetDirectories()
                        |> Seq.sumBy ( fun folder -> getFolderSize cancel folder folderSizeMap )

                    let folderSize = allFilesSize + allSubFolderSize

                    folderSizeMap.AddOrUpdate(dirInfo.FullName, folderSize, (fun k ev-> ev) ) |> ignore

                    folderSize
        with
            | ex -> 
                File.AppendAllText( "log.txt", ex.ToString() ) 
                0L

       
    type IProgressContext =
        abstract member UpdateProgress : double->unit
        abstract member UpdateStatus: string->unit
        abstract member Finish: unit->unit
        abstract member Canceled : bool with get, set
        abstract member FoundAClone : CloneGroup -> unit