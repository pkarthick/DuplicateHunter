


using DuplicateHunter.ViewModel;
using DuplicateHunter.Helpers;
using System.Windows;
using System.Windows.Controls;

namespace DuplicateHunter
{
    /// <summary>
    /// Interaction logic for ExplorerTree.xaml
    /// </summary>
    public partial class ExplorerTree : UserControl
    {
        public ExplorerTree()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue != null)
            {
                MainWindowViewModel mwvm = this.DataContext as MainWindowViewModel;
                mwvm.SelectedFolderItem = (e.NewValue as FolderInfo).FullName;
            }
        }
    }
}
