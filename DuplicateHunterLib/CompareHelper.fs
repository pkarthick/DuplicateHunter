#light

module public DuplicateHunterLib

open System
open System.IO
open System.Threading
open Microsoft.FSharp.Collections
open System.Collections.Generic
open System.Collections.Concurrent
open System.Collections.ObjectModel

open DuplicateHunter.Model

//type System.IO.DirectoryInfo with 
//    
//    member public this.GetSize() : int64 = 
//
//        let folderSizeMap = new Dictionary<string, int64>()
//
//        let rec getFolderSize() = 
//
//            match folderSizeMap.ContainsKey(this.FullName) with 
//            | true -> folderSizeMap.[this.FullName]
//            | false ->
//                let allFilesSize = 
//                    this.GetFiles()
//                    |> Seq.sumBy (fun file -> file.Length)
//
//                let allSubFolderSize = 
//                    this.GetDirectories()
//                    |> Seq.sumBy ( fun folder -> folder.GetSize() )
//
//                let folderSize = allFilesSize + allSubFolderSize
//
//                folderSizeMap.Add(this.FullName, folderSize)
//
//                folderSize
//
//        getFolderSize()



let largeFile sizeToCompare (fileItem:FileItem) =
    fileItem.Size > sizeToCompare 

let largeFolder sizeToCompare (folderItem:FolderItem) =
    folderItem.Size > sizeToCompare

let private testAllPredicates predicateArgument predicateList =
    predicateList
    |> List.forall ( fun predicate -> predicate(predicateArgument) )

let private createDeltaItem item itemType left right deltaType = 
    { ItemType = itemType; DeltaType = deltaType; FileName = item; LeftFolder = left; RightFolder = right; DeltaItems=[]; }

let rec CompareFolder item left right =
        
    let getFileDeltaType file left right =
        let leftFile, rightFile = Path.Combine(left,file), Path.Combine(right,file)
        match File.Exists(leftFile), File.Exists(rightFile) with
        | true, true ->
            match (new FileInfo(leftFile)).Length = (new FileInfo(rightFile)).Length with
            | true -> DeltaType.Identical
            | false -> DeltaType.Different
        | true, false -> DeltaType.LeftOnly
        | false, true -> DeltaType.RightOnly
        | false, false -> failwith "File does not exist in both the directories, huh? How could this ever happen?!?!?!"
            
    let processFolder folder =
        let childLeft, childRight = Path.Combine(left,folder), Path.Combine(right,folder)
        CompareFolder folder childLeft childRight

    let processFile file =
        createDeltaItem file ItemType.File left right (getFileDeltaType file left right)
            
    let allIdentical deltaItems = deltaItems |> List.forall (fun deltaItem -> deltaItem.DeltaType = DeltaType.Identical) 
        
    let allDeltaItems itemType itemsGetter processItem =
        let getDeltaItems itemType items deltaType = 
            let leftFolder, rightFolder =
                match deltaType with
                | DeltaType.RightOnly -> String.Empty, right
                | DeltaType.LeftOnly -> left, String.Empty
                | _ -> left, right
                    
            items 
            |> Set.map (fun file -> createDeltaItem file itemType leftFolder rightFolder deltaType)
            |> Set.toList

        let createItemSet folderName itemsGetter = folderName |> itemsGetter |> Array.map Path.GetFileName |> Set.ofArray  
        let leftItems = createItemSet left itemsGetter
        let rightItems = createItemSet right itemsGetter
            
        let commonItems = Set.intersect leftItems rightItems
        let leftOnlyItems = leftItems - commonItems
        let rightOnlyItems = rightItems - commonItems
            
        let leftOnlyDeltaItems = getDeltaItems itemType leftOnlyItems DeltaType.LeftOnly
        let rightOnlyDeltaItems = getDeltaItems itemType rightOnlyItems DeltaType.RightOnly

        let commonDeltaItems processItem commonItems = 
            commonItems 
            |> Set.map processItem 
            |> Set.toList
            
        let commonItems = commonDeltaItems processItem commonItems
        commonItems @ leftOnlyDeltaItems @ rightOnlyDeltaItems
        
    let allFolderItems = allDeltaItems ItemType.Folder Directory.GetDirectories processFolder
    let allFileItems = allDeltaItems ItemType.File Directory.GetFiles processFile
        
    let deltaType = 
        match (allIdentical allFolderItems) && (allIdentical allFileItems) with
        | true -> DeltaType.Identical
        | false -> DeltaType.Different
        
    { ItemType = ItemType.Folder; DeltaType = deltaType; FileName = item; LeftFolder = left; RightFolder = right; DeltaItems=allFolderItems @ allFileItems; }



let ProcessFolderItem 
    (folderItem:FolderItem)
    (folderPredicateList : (FolderItem->bool) list) 
    (nameMap : ConcurrentDictionary<string,  list<Item>>)
    (sizeMap : ConcurrentDictionary<int64,  list<Item>>) =
    
//    let folderItem = GetFolderItem dirInfo processFoldersOnly (*isSizeSearch sizeInBytes includeEmptyFolders includeEmptyFiles*) filePredicateList folderPredicateList nameMap sizeMap 
    let pass = testAllPredicates folderItem folderPredicateList
    
    if pass = true then
        nameMap.AddOrUpdate( (Path.GetFileName(folderItem.FullName)), [Folder(folderItem)], (fun k ev-> Folder(folderItem)::ev) ) |> ignore
        //fun (Folder(fi))-> fi.FullName.StartsWith(folderItem.FullName)) sizeMap.[folderItem.Size]
        match sizeMap.ContainsKey(folderItem.Size) with
        | false -> sizeMap.AddOrUpdate( folderItem.Size, [Folder(folderItem)], (fun k ev-> Folder(folderItem)::ev) ) |> ignore
        | true ->
            if List.exists 
                (
                    fun (item) -> 
                        match (item) with 
                        | Folder(foldi) -> foldi.FullName.StartsWith(folderItem.FullName)
                        | File(filei) -> false
                ) 
                sizeMap.[folderItem.Size] = false then
                sizeMap.AddOrUpdate( folderItem.Size, [Folder(folderItem)], (fun k ev-> Folder(folderItem)::ev) ) |> ignore
    

let rec GetFolderItem   
                        (progressContext: IProgressContext)
                        (dirInfo : System.IO.DirectoryInfo)
                        (foldersToExclude : seq<System.String>) 
                        (foldersOnly:bool) 
                        (isSizeSearch:bool) 
                        (sizeInBytes:Int64) 
                        (filePredicateList : (FileItem->bool) list) 
                        (folderPredicateList : (FolderItem->bool) list) 
                        (nameMap : ConcurrentDictionary<string, list<Item>>)
                        (sizeMap : ConcurrentDictionary<int64, list<Item>>) 
                        (folderSizeMap : ConcurrentDictionary<string, int64>) =
    

    try
        if progressContext.Canceled = false && (Seq.exists (fun folderToExclude -> dirInfo.FullName.Contains(folderToExclude)) foldersToExclude) = false then

          let status = String.Format("Analyzing folder: {0}", dirInfo.FullName)
          progressContext.UpdateStatus status

          let folderSize = getFolderSize progressContext.Canceled dirInfo folderSizeMap
        
          match isSizeSearch && folderSize >= sizeInBytes with
          | true ->
              let filesItem = 
                  match foldersOnly with
                  | true -> []
                  | false ->
                      dirInfo.GetFiles() 
                      |> Array.map (fun fileInfo -> new FileItem(fileInfo) )
                      |> Array.toList

              if List.length filesItem > 1 then
                  filesItem
                  |> List.filter (fun fileItem -> testAllPredicates fileItem filePredicateList ) 
                  |> List.iter (fun fileItem -> 
                                  sizeMap.AddOrUpdate( fileItem.Size, [File(fileItem)], (fun k ev-> File(fileItem)::ev) ) |> ignore 
                                  nameMap.AddOrUpdate( fileItem.FullName, [File(fileItem)], (fun k ev-> File(fileItem)::ev) ) |> ignore 
                               )

              let foldersItem =
                  dirInfo.GetDirectories() 
                  |> PSeq.map (fun dirInfo -> GetFolderItem progressContext dirInfo foldersToExclude foldersOnly isSizeSearch sizeInBytes filePredicateList folderPredicateList nameMap sizeMap folderSizeMap )
                  |> PSeq.toList
                  //|> Seq.filter (fun folderItem -> testAllPredicates folderItem folderPredicateList ) 
        
              foldersItem
              |> Seq.iter (fun folderItem -> ProcessFolderItem folderItem folderPredicateList nameMap sizeMap)

              {Name = dirInfo.Name; IsSelected=false; IsClone = false; FullName = dirInfo.FullName; Size = folderSize; DisplaySize = toReadableSize folderSize; Files = filesItem; SubFolders = List.ofSeq foldersItem;}

              |false -> { Name = dirInfo.Name; IsSelected=false; IsClone = false; FullName = dirInfo.FullName; Size = folderSize; DisplaySize = toReadableSize folderSize; Files = []; SubFolders = [] }
        else
            { Name = dirInfo.Name; IsSelected=false; IsClone = false; FullName = dirInfo.FullName; Size = 0L; DisplaySize = toReadableSize 0L; Files = []; SubFolders = [] }
    with
        | ex -> {Name = dirInfo.Name; IsSelected=false; IsClone = false; FullName = dirInfo.FullName; Size = 0L; DisplaySize = toReadableSize 0L; Files = []; SubFolders = []}

let HuntClones  (progressContext: IProgressContext)
                (syncContext: SynchronizationContext)
                (foldersToHunt : seq<System.String>) 
                (foldersToExclude : seq<System.String>) 
                (foldersOnly:bool) 
                (isSizeSearch:bool) 
                (sizeInBytes:Int64) 
                (includeEmptyFolders:bool) 
                (includeEmptyFiles:bool) =
                //(cloneGroups : ConcurrentBag<CloneGroup>) =
    
    let nameMap = new ConcurrentDictionary<string, list<Item>>()
    let sizeMap = new ConcurrentDictionary<int64, list<Item>>()
    let folderSizeMap = new ConcurrentDictionary<string, int64>()
    let cloneGroups = new ConcurrentBag<CloneGroup>()

    let startTime = DateTime.Now

    let filePredicateList = 
        match isSizeSearch with
        | true -> [(largeFile sizeInBytes)]
        | false -> []

    let folderPredicateList = 
        match isSizeSearch with
        | true -> [(largeFolder sizeInBytes)]
        | false -> []

    let processedFolders =
        foldersToHunt
        |> PSeq.map (fun folder -> 
                        let dirInfo = new DirectoryInfo(folder)
                        let folderItem = GetFolderItem progressContext dirInfo foldersToExclude foldersOnly isSizeSearch sizeInBytes (*includeEmptyFolders includeEmptyFiles*) filePredicateList folderPredicateList nameMap sizeMap folderSizeMap
                        ProcessFolderItem folderItem folderPredicateList nameMap sizeMap 
                        let pass = testAllPredicates folderItem folderPredicateList
    
                        if pass = true then
                            nameMap.AddOrUpdate( (Path.GetFileName(folderItem.FullName)), [Folder(folderItem)], (fun k ev-> Folder(folderItem)::ev) ) |> ignore
                            sizeMap.AddOrUpdate( folderItem.Size, [Folder(folderItem)], (fun k ev-> Folder(folderItem)::ev) ) |> ignore

                        folderItem
                   )
        |> PSeq.toList

    if not progressContext.Canceled then

        let timeTaken = startTime - DateTime.Now

        let onlyFolders items = 
            items
            |> Seq.collect (fun dataElement -> 
                                seq{
                                    match dataElement with
                                    | File (fileItem) -> ()
                                    | Folder (folderItem) -> yield folderItem
                                }
                            )
            |> Seq.toList

        let onlyFiles items = 
            items
            |> Seq.collect (fun dataElement -> 
                                seq{
                                    match dataElement with
                                    | File (fileItem) -> yield fileItem
                                    | Folder (folderItem) -> ()
                                }
                            )
            |> Seq.toList
    
        let filterFolderClones onlyFolders (clonesFound:List<FolderItem>) =

            let isClone leftFolder rightFolder =
                let delta = CompareFolder leftFolder.Name leftFolder.FullName rightFolder.FullName
                delta.DeltaType = DeltaType.Identical

            onlyFolders
            |> Seq.iter (fun leftFolder ->
                        
                            // if parent folder is a clone then exclude child folder
                            if( progressContext.Canceled = false && Seq.exists (fun clone-> leftFolder.FullName.StartsWith(clone.FullName)) clonesFound ) = false then
                            //if clonesFound.Contains(leftFolder) = false then
                                let cloneFolderItems = 
                                    onlyFolders 
                                    |> PSeq.filter (fun rightFolder -> progressContext.Canceled = false && (leftFolder <> rightFolder) && (leftFolder.Size <> 0L) && (isClone leftFolder rightFolder))
                                    |> PSeq.toList

                                if  progressContext.Canceled = false && cloneFolderItems.IsEmpty = false  && cloneFolderItems.Length > 0 then
                                    let cloneFolderItems1 = leftFolder :: cloneFolderItems

                                    clonesFound.AddRange cloneFolderItems1

                                    clonesFound |> Seq.iter (fun cl -> cl.IsClone <- true)

                                    let cloneGroup = CloneGroup()

                                    let cloneItems = 
                                        cloneFolderItems1 
                                        |> Seq.map (fun folderItem -> cloneGroup.AddItem(folderItem.FullName, folderItem.Size))
                                        |> Seq.toList

                                    syncContext.Post(
                                        (fun _ -> 
                                            progressContext.FoundAClone cloneGroup
                                            cloneGroups.Add (cloneGroup)
                                        ), null
                                    )
                        )

        let filterFileClones (onlyFiles:FileItem list) (folders:string list) =
        
            let folderList = new List<System.String>(folders)

            let allFilesContainedByFolderList =
                onlyFiles
                |> List.forall (fun (fileItem:FileItem) -> folderList.Contains(fileItem.Path)  )

            let cloneGroup = CloneGroup()

            //if Seq.exists (fun (fileItem:FileItem) -> folderList.Contains(fun folderPath -> fileItem.Path.Contains( folderPath ) ) = false ) onlyFiles then
            if progressContext.Canceled = false && allFilesContainedByFolderList = false then
                let fileItems =
                    onlyFiles
                    |> Seq.map (fun fileItem -> cloneGroup.AddItem(fileItem.FullName, fileItem.Size))
                    |> Seq.toList

                onlyFiles |>  Seq.iter (fun cl -> cl.IsClone <- true)
                
                if progressContext.Canceled = false && fileItems.Length > 0 then
                    syncContext.Post(
                                        (fun _ -> 
                                            progressContext.FoundAClone cloneGroup
                                            cloneGroups.Add (cloneGroup)
                                        ), null
                                    )

        //let filterParentWithOnlyChild 
        if not progressContext.Canceled then

            let clonesFound = new List<FolderItem>()

            sizeMap
            |> Seq.filter (fun kv -> List.length kv.Value > 1)
            |> Seq.sortBy (fun kv -> kv.Key)
            |> Seq.rev
            |> Seq.iter (fun kv -> filterFolderClones (onlyFolders kv.Value) clonesFound)

            let folderList = new List<string>()
    
            if not progressContext.Canceled then

                let folders = 
                    cloneGroups
                    |> Seq.map (fun cloneGroup -> cloneGroup.CloneItems)
                    |> Seq.concat 
                    |> Seq.map (fun item -> if item.IsFolder then item.Path else item.ParentFolder)
                    |> Seq.toList
              
                let x = folders.Length

                if progressContext.Canceled = false && foldersOnly = false then
                    sizeMap
                    |> Seq.filter (fun kv -> List.length kv.Value > 1)
                    |> Seq.iter (fun kv -> filterFileClones (onlyFiles kv.Value) (folders))

    if progressContext.Canceled then
        progressContext.UpdateStatus "Canceled!!!"
    else
        let message = System.String.Format( "{0} clones have been hunted!!!", cloneGroups.Count)
        progressContext.UpdateStatus message

    new List<FolderItem>(processedFolders)


let private dummy = 
    
    let checkExtension (includeExtensions: string list) (excludeExtensions: string list) (fileItem:FileItem) =
        ( 
            includeExtensions
            |> List.exists ( fun extension -> System.String.Compare( Path.GetExtension(fileItem.Name), extension, true ) = 0 )
        ) 
        ||
        ( 
            excludeExtensions
            |> List.exists ( fun extension -> System.String.Compare( Path.GetExtension(fileItem.Name), extension, true ) <> 0 )
        )

//    let filePredicateList =
//        [  checkExtension [".exe"; ".txt"] [ ".pdb"] ; (largeFiles (4L * 1024L)); ]
//
//    let folderPredicateList = 
//        [ largeFolders (100L * 1024L); ]

    let nameMap = new ConcurrentDictionary<string, list<Item>>()

    let sizeMap = new ConcurrentDictionary<int64, list<Item>>()


    let di1 = new DirectoryInfo( @"F:\" )
    let di2 = new DirectoryInfo( @"C:\" )

    //let size = di1.GetSize()

    //let x1 = GetFolderItem di1 filePredicateList folderPredicateList nameMap sizeMap

    //let x2 = GetFolderItem di2 filePredicateList folderPredicateList nameMap sizeMap

    Console.WriteLine "Press any key to continue..."

    Console.ReadKey()
    