namespace DuplicateHunter.ViewModel

open System
open System.Windows


[<Measure>]
type bytes

[<Measure>]
type KB

[<Measure>]
type MB

[<Measure>]
type GB
//
//
//type BoolToVisibilityConverter =
//
//    interface IValueConverter with
//
//        member x.Convert(value, targetType, parameter, culture) = 
//
//            let mutable x : obj = upcast Visibility.Collapsed
//
//            match value :?> System.Boolean with 
//            | true -> x <- Visibility.Visible
//            | false -> x <- Visibility.Collapsed 
//
//            x
//
//        member x.ConvertBack(value, targetType, parameter, culture) = 
//            raise <| NotImplementedException()
//            
