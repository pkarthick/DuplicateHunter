using DuplicateHunter;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DuplicateHunter
{

    public class DebugConverter : IValueConverter
    {
        
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value;
        }
    }

    /// <summary>
    /// Implements the conversion between a <code>bool?</code> 
    /// and the <code>Visibility</code> enum:
    /// null  = Hidden
    /// false = Collapsed
    /// true  = Visible
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool? visible = (bool?)value;
            if (visible.HasValue)
                return (visible.Value ? Visibility.Visible : Visibility.Collapsed);
            else
                return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Visibility vis = (Visibility)value;
            switch (vis)
            {
                case Visibility.Collapsed:
                    return false;
                case Visibility.Hidden:
                    return null;
                case Visibility.Visible:
                    return true;
            }
            return null;
        }

        #endregion
    }

    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                if (System.Convert.ToBoolean(value))
                {
                    return Brushes.Red;
                }
            }

            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FoldersFilesConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var list = new List<object> {  };

            var folder = values[0] as Model.FolderItem;

            if (folder != null && !folder.IsClone)
            {
                if (values[1] != DependencyProperty.UnsetValue)
                    list.AddRange(values[1] as Microsoft.FSharp.Collections.FSharpList<Model.FolderItem>);

                if (values[2] != DependencyProperty.UnsetValue)
                    list.AddRange(values[2] as Microsoft.FSharp.Collections.FSharpList<Model.FileItem>);
            }

            return list;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class NotConverter : IValueConverter
    {
        #region IValueConverter Members

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof(bool) && targetType != typeof(bool?))
                throw new InvalidOperationException("The target must be a boolean");
            
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion
    }

}
