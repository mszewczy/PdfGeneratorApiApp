using PdfGeneratorApiApp.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace PdfGeneratorApiApp.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.SelectedItem = e.NewValue as Models.TocItem;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                if (viewModel.IsDirty)
                {
                    var result = MessageBox.Show("Czy chcesz zapisać zmiany w pliku PDF?", "Zapisz zmiany", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Użyj istniejącej komendy do generowania/zapisu PDF
                        if (viewModel.GeneratePdfAsyncCommand.CanExecute(null))
                        {
                            viewModel.GeneratePdfAsyncCommand.Execute(null);
                        }
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                // POPRAWKA: Dodano prawidłowe zarządzanie zasobami przez wywołanie Dispose()
                if (!e.Cancel)
                {
                    viewModel.Dispose();
                }
            }
        }
    }
}