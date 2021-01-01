using DuplicateHunter.ViewModel;
using DuplicateHunter;
using MahApps.Metro.Controls;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static DuplicateHunter.Model;
using System.Xml.Linq;
using System.Collections.Generic;
using Microsoft.Win32;
using System;

namespace DuplicateHunter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, Model.IProgressContext
    {

        private string winmergePath = ConfigurationManager.AppSettings["winmerge_path"];

        private MainWindowViewModel mainWindowViewModel = null;

        public MainWindow()
        {
            InitializeComponent();
            mainWindowViewModel = new MainWindowViewModel(this);
            DataContext = mainWindowViewModel;
        }

        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SearchOptionsFlyout.IsOpen = true;
            //SearchOptionsToggleButton.Visibility = Visibility.Collapsed;
        }

        private void SearchOptionsFlyout_ClosingFinished(object sender, RoutedEventArgs e)
        {
            //SearchOptionsToggleButton.Visibility = Visibility.Visible;
        }


        public void UpdateProgress(double progress)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background,
                (SendOrPostCallback)delegate { Progress.SetValue(ProgressBar.ValueProperty, progress); }, null);
        }

        public void UpdateStatus(string status)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background,
                (SendOrPostCallback)delegate { StatusText.SetValue(TextBlock.TextProperty, status); }, null);
        }

        public void Finish()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background,
                (SendOrPostCallback)delegate { Close(); }, null);
        }

        public bool Canceled
        {
            get;
            set;
        }


        public void FoundAClone(Model.CloneGroup value)
        {
            mainWindowViewModel.Clones.Add(value);
        }
        
        private void CompareFilesButton_Click(object sender, RoutedEventArgs e)
        {
            ObservableCollection<object> selectedItemCollection = ((Button)sender).CommandParameter as ObservableCollection<object>;

            if (selectedItemCollection != null && selectedItemCollection.Count == 2)
            {

                Model.Item.File file1 = selectedItemCollection.ElementAt(0) as Model.Item.File;
                Model.Item.File file2 = selectedItemCollection.ElementAt(1) as Model.Item.File;

                if (file1 != null && file2 != null)
                {
                    Process.Start(winmergePath, "\"" + file1.Item.FullName + "\" \"" + file2.Item.FullName + "\"");
                }
                else
                {
                    Model.Item.Folder folder1 = selectedItemCollection.ElementAt(0) as Model.Item.Folder;
                    Model.Item.Folder folder2 = selectedItemCollection.ElementAt(1) as Model.Item.Folder;

                    if (folder1 != null && folder2 != null)
                    {
                        Process.Start(winmergePath, "\"" + folder1.Item.FullName + "\" \"" + folder2.Item.FullName + "\"");
                    }
                }
            }
            
        }

        private void CompareFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            ObservableCollection<object> selectedItemCollection = ((Button)sender).CommandParameter as ObservableCollection<object>;

            if (selectedItemCollection != null && selectedItemCollection.Count == 2)
            {
                
                Model.Item.File file1 = selectedItemCollection.ElementAt(0) as Model.Item.File;
                Model.Item.File file2 = selectedItemCollection.ElementAt(1) as Model.Item.File;

                if (file1 != null && file2 != null)
                {

                    string directory1 = System.IO.Path.GetDirectoryName(file1.Item.FullName);
                    string directory2 = System.IO.Path.GetDirectoryName(file2.Item.FullName);

                    Process.Start(winmergePath, "\"" + directory1 + "\" \"" + directory2 + "\"");

                }
                else
                {
                    Model.Item.Folder folder1 = selectedItemCollection.ElementAt(0) as Model.Item.Folder;
                    Model.Item.Folder folder2 = selectedItemCollection.ElementAt(1) as Model.Item.Folder;

                    if (folder1 != null && folder2 != null)
                    {
                        Process.Start(winmergePath, "\"" + System.IO.Path.GetDirectoryName(folder1.Item.FullName) + "\" \"" + System.IO.Path.GetDirectoryName(folder2.Item.FullName) + "\"");
                    }
                }
            }
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            string selectedItem = ((Button)sender).CommandParameter as string;

            if (selectedItem != null)
            {
                if (File.Exists(selectedItem) || Directory.Exists(selectedItem))
                    Process.Start("\"" + selectedItem + "\"");
            }
            
        }


        private void OpenFolderItem_Click(object sender, RoutedEventArgs e)
        {
            string selectedItem = ((Button)sender).CommandParameter as string;

            if (selectedItem != null)
            {
                if (File.Exists(selectedItem) || Directory.Exists(selectedItem))
                    Process.Start("\"" + Path.GetDirectoryName(selectedItem) + "\"");
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {

            string selectedItem = ((Button)sender).CommandParameter as string;

            if (selectedItem != null)
            {
                if (Directory.Exists(selectedItem))
                {
                    MessageBoxResult result = MessageBox.Show($"Are you sure you want to delete '{selectedItem}' and its subdirectories?", "Delete Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        Directory.Delete(selectedItem, true);
                    }
                }
                else if (File.Exists(selectedItem))
                {
                    MessageBoxResult result = MessageBox.Show($"Are you sure you want to delete '{selectedItem}'?", "Delete Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        File.Delete(selectedItem);
                    }
                }
                
            }

            mainWindowViewModel.Refresh();

        }

        private void RefreshResults_Click(object sender, RoutedEventArgs e)
        {
            mainWindowViewModel.Refresh();
        }

        //private void RefreshClones()
        //{
        //    for (int i = mainWindowViewModel.Clones.Count - 1; i >= 0; i--)
        //    {
        //        var cloneGroup = mainWindowViewModel.Clones.ElementAt(i);

        //        for (int j = cloneGroup.Clones.Count - 1; j >= 0; j--)
        //        {
        //            if (cloneGroup.Clones[j].IsFolder)
        //            {
        //                Model.Item.Folder folder1 = cloneGroup.Clones[j] as Model.Item.Folder;

        //                if (folder1 != null)
        //                {
        //                    if (!Directory.Exists(folder1.Item.FullName))
        //                    {
        //                        cloneGroup.Clones.Remove(folder1);
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                Model.Item.File file1 = cloneGroup.Clones[j] as Model.Item.File;

        //                if (file1 != null)
        //                {
        //                    if (!File.Exists(file1.Item.FullName))
        //                    {
        //                        cloneGroup.Clones.Remove(file1);
        //                    }
        //                }
        //            }

        //        }

        //        if (cloneGroup.Clones.Count < 2)
        //        {
        //            mainWindowViewModel.Clones.Remove(cloneGroup);
        //        }

        //    }
        //}


        private void SaveResults_Click(object sender, RoutedEventArgs e)
        {

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.InitialDirectory = Directory.GetCurrentDirectory();
            dialog.Filter = "Clones (*.clones.xml)|*.clones.xml";
            dialog.FileName = Guid.NewGuid().ToString();

            if (dialog.ShowDialog().GetValueOrDefault())
            {
               new XElement( "Root", 
                   mainWindowViewModel
                    .Clones
                    .Select(
                       cl => 
                        new XElement
                        ("CloneGroup",
                        cl.CloneItems.Select(item => new XElement("CloneItem", item.Path, new XAttribute( "Size", item.Size ) )
                        )
                   ))).Save( dialog.FileName );
            }
            

        }
        
        private void Button_OpenResultClones_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.InitialDirectory = Directory.GetCurrentDirectory();
            dialog.Filter = "Clones (*.clones.xml)|*.clones.xml";

            if (dialog.ShowDialog().GetValueOrDefault())
            {
                XElement rootElement = XElement.Load(dialog.FileName);
                                
                var cloneGroups = new List<CloneGroup>();

                foreach (var cg in rootElement
                    .Descendants("CloneGroup"))
                {
                    cloneGroups.Add(CreateCloneGroup(cg));
                }

                mainWindowViewModel.Clones.Clear();
                cloneGroups.ForEach(mainWindowViewModel.Clones.Add);

                UpdateStatus("");

                mainWindowViewModel.IsSearchResultsTabVisible = true;

            }
        }

        private CloneGroup CreateCloneGroup(XElement cloneGroupElement)
        {
            var cg = new CloneGroup();

            cloneGroupElement
                .Descendants("CloneItem")
                .ToList()
                .ForEach(ci =>
                {
                    cg.AddItem(ci.Value, ci.Attribute("Size") == null ? 0 : Convert.ToInt64(ci.Attribute("Size").Value));

                });

            return cg;
        }


    }
}
