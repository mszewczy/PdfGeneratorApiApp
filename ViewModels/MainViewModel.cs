using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GongSolutions.Wpf.DragDrop;
using Microsoft.Win32;
using PdfGeneratorApiApp.Models;
using PdfGeneratorApiApp.Services;
using Syncfusion.Pdf.Interactive; // POPRAWKA: Dodano brakującą przestrzeń nazw dla PdfBookmarkCollection i IPdfBookmark.
using Syncfusion.Pdf.Parsing;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace PdfGeneratorApiApp.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDropTarget
    {
        public ObservableCollection<TocItem> TocItems { get; } = new();

        [ObservableProperty]
        private Stream? _pdfDocumentStream;

        [ObservableProperty]
        private string _newItemUrl = "https://www.dynamicpdf.com/";

        [ObservableProperty]
        private string _newItemDisplayText = "Nowy Element";

        [ObservableProperty]
        private bool _isTocAtStart = true;

        [ObservableProperty]
        private bool _addToc = true;

        [ObservableProperty]
        private bool _generateQrCodeTable = true;

        [ObservableProperty]
        // USUNIĘTO: Atrybut [NotifyCanExecuteChangedFor] był zbędny.
        // CommunityToolkit.Mvvm automatycznie powiadamia o zmianie CanExecute,
        // ponieważ metoda CanGeneratePdf() zależy od właściwości IsGenerating.
        private bool _isGenerating = false;

        [ObservableProperty]
        // USUNIĘTO: Atrybuty [NotifyCanExecuteChangedFor] były zbędne z tego samego powodu co powyżej.
        // Metody CanExecute dla komend (np. CanAddSubItem) zależą od właściwości SelectedItem.
        private TocItem? _selectedItem;

        [ObservableProperty]
        private bool _isDirty = false;

        public MainViewModel()
        {
        }

        [RelayCommand]
        private void AddItem()
        {
            if (!string.IsNullOrWhiteSpace(NewItemDisplayText))
            {
                TocItems.Add(new TocItem { Url = NewItemUrl, DisplayText = NewItemDisplayText });
                NewItemUrl = string.Empty;
                NewItemDisplayText = string.Empty;
                IsDirty = true;
            }
        }

        private bool CanAddSubItem() => SelectedItem != null;

        [RelayCommand(CanExecute = nameof(CanAddSubItem))]
        private void AddSubItem()
        {
            if (SelectedItem != null)
            {
                var newItem = new TocItem { DisplayText = "Nowy Pod-element", Parent = SelectedItem };
                SelectedItem.Children.Add(newItem);
                SelectedItem.IsExpanded = true;
                IsDirty = true;
            }
        }

        private bool CanRemoveItem() => SelectedItem != null;

        [RelayCommand(CanExecute = nameof(CanRemoveItem))]
        private void RemoveItem()
        {
            if (SelectedItem != null)
            {
                (SelectedItem.Parent?.Children ?? TocItems).Remove(SelectedItem);
                IsDirty = true;
            }
        }

        private bool CanStartEditItem() => SelectedItem != null;

        [RelayCommand(CanExecute = nameof(CanStartEditItem))]
        private void StartEditItem()
        {
            if (SelectedItem != null)
            {
                SelectedItem.IsEditing = true;
            }
        }

        [RelayCommand]
        private void EndEditItem()
        {
            if (SelectedItem != null)
            {
                SelectedItem.IsEditing = false;
                IsDirty = true;
            }
        }

        private bool CanGeneratePdf() => !IsGenerating;

        [RelayCommand(CanExecute = nameof(CanGeneratePdf))]
        private async Task GeneratePdfAsync()
        {
            if (TocItems.Count == 0)
            {
                MessageBox.Show("Lista spisu treści jest pusta.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsGenerating = true;
            try
            {
                var pdfService = new DynamicPdfApiService();
                PdfDocumentStream?.Dispose();
                PdfDocumentStream = await pdfService.GeneratePdfAsync(TocItems, IsTocAtStart, GenerateQrCodeTable, AddToc);
                MessageBox.Show("Dokument PDF został wygenerowany pomyślnie.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                IsDirty = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd podczas generowania PDF: {ex.Message}", "Błąd Krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsGenerating = false;
            }
        }

        [RelayCommand]
        private void OpenPdf()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var fileStream = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read);
                    var loadedDocument = new PdfLoadedDocument(fileStream);

                    TocItems.Clear();
                    // POPRAWKA: Używamy właściwości Bookmarks, która jest typu PdfBookmarkCollection.
                    LoadBookmarks(loadedDocument.Bookmarks, TocItems, null);

                    PdfDocumentStream?.Dispose();
                    // Ponowne wczytanie pliku do MemoryStream dla PdfViewerControl
                    PdfDocumentStream = new MemoryStream(File.ReadAllBytes(openFileDialog.FileName));
                    IsDirty = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Wystąpił błąd podczas otwierania pliku PDF: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadBookmarks(PdfBookmarkCollection loadedBookmarks, ObservableCollection<TocItem> tocItems, TocItem? parent)
        {
            foreach (IPdfBookmark loadedBookmark in loadedBookmarks)
            {
                var tocItem = new TocItem
                {
                    DisplayText = loadedBookmark.Title,
                    Parent = parent
                    // Uwaga: Wczytywanie URL z zakładki wymagałoby bardziej zaawansowanej logiki,
                    // ponieważ zakładki mogą prowadzić do różnych typów akcji, nie tylko URI.
                    // Na razie pozostawiamy URL pusty.
                };

                tocItems.Add(tocItem);
                if (loadedBookmark.InnerBookmarks.Count > 0)
                {
                    LoadBookmarks(loadedBookmark.InnerBookmarks, tocItem.Children, tocItem);
                }
            }
        }

        #region Implementacja IDropTarget
        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is not TocItem sourceItem) return;

            dropInfo.Effects = DragDropEffects.None;

            if (dropInfo.TargetItem is TocItem targetItem)
            {
                if (!IsDescendant(sourceItem, targetItem))
                {
                    dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                    dropInfo.Effects = DragDropEffects.Move;
                }
            }
            else
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is not TocItem sourceItem) return;

            (sourceItem.Parent?.Children ?? TocItems).Remove(sourceItem);

            if (dropInfo.TargetItem is TocItem targetItem)
            {
                // Wstawianie jako dziecko
                targetItem.Children.Add(sourceItem);
                sourceItem.Parent = targetItem;
                targetItem.IsExpanded = true;
            }
            else
            {
                // Wstawianie na liście głównej
                int insertIndex = dropInfo.InsertIndex;
                if (insertIndex < 0) insertIndex = 0;
                if (insertIndex > TocItems.Count) insertIndex = TocItems.Count;

                TocItems.Insert(insertIndex, sourceItem);
                sourceItem.Parent = null;
            }
            IsDirty = true;
        }

        private static bool IsDescendant(TocItem source, TocItem target)
        {
            TocItem? current = target;
            while (current != null)
            {
                if (current == source) return true;
                current = current.Parent;
            }
            return false;
        }
        #endregion
    }
}
