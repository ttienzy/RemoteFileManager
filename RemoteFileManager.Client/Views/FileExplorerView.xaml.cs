using RemoteFileManager.Client.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RemoteFileManager.Client.Views
{
    /// <summary>
    /// Interaction logic for FileExplorerView.xaml
    /// </summary>
    public partial class FileExplorerView : UserControl
    {
        public FileExplorerView()
        {
            InitializeComponent();
        }
        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 1. Lấy ra ListBoxItem vừa được click
            if (sender is ListBoxItem item && item.Content != null)
            {
                // 2. Lấy ViewModel hiện tại của màn hình
                if (DataContext is FileExplorerViewModel viewModel)
                {
                    // 3. Gọi lệnh OpenItemCommand trong ViewModel
                    // item.Content chính là FileDto hoặc DriveDto
                    if (viewModel.OpenItemCommand.CanExecute(item.Content))
                    {
                        viewModel.OpenItemCommand.Execute(item.Content);
                    }
                }
            }
        }
    }
}
