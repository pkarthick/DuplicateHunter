namespace DuplicateHunter.Helpers

open System
open System.IO
open System.Collections.Concurrent
open Microsoft.FSharp.Collections


type FolderInfo (dirInfo : DirectoryInfo)  =

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


    let rec getFolderSize (dirInfo:DirectoryInfo) (folderSizeMap:ConcurrentDictionary<string, int64>) = 

        match folderSizeMap.ContainsKey(dirInfo.FullName) with 
        | true -> folderSizeMap.[dirInfo.FullName]
        | false ->
            let allFilesSize = 
                dirInfo.EnumerateFiles( "*", SearchOption.AllDirectories )
                |> Seq.sumBy (fun file -> file.Length)
//
//            let allSubFolderSize = 
//                dirInfo.GetDirectories()
//                |> PSeq.sumBy ( fun folder -> getFolderSize folder folderSizeMap )

            //let folderSize = allFilesSize + allSubFolderSize

            folderSizeMap.AddOrUpdate(dirInfo.FullName, allFilesSize, (fun k ev-> ev) ) |> ignore

            allFilesSize

    let mutable d = dirInfo

    member x.Name =
        d.Name        

    member x.FullName =
        d.FullName

    static member FolderSizeMap with get() = new ConcurrentDictionary<string, int64>()

    member x.Size =
        ""
        //toReadableSize (getFolderSize d FolderInfo.FolderSizeMap)

    member x.Children =
        try
            dirInfo.GetDirectories()
            |> Seq.map (fun childDir -> new FolderInfo(childDir))
        with
        | :? System.UnauthorizedAccessException -> Seq.empty

    static member Drives 
        with get() =        
            DriveInfo.GetDrives()
            |> Seq.filter (fun drive -> drive.IsReady)
            |> Seq.map (fun drive -> new FolderInfo(drive.RootDirectory))    

