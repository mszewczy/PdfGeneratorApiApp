using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GongSolutions.Wpf.DragDrop;
using PdfGeneratorApiApp.Models;
using PdfGeneratorApiApp.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace PdfGeneratorApiApp.ViewModels
{
    // Klasa musi być 'partial', aby generatory kodu mogły ją rozszerzyć.
    public partial class MainViewModel : ObservableObject, IDropTarget
    {
        // Użycie wyrażenia kolekcji z C# 12 do inicjalizacji.
        public ObservableCollection<TocItem> TocItems { get; } = [];

        // Atrybut [ObservableProperty] automatycznie generuje publiczną właściwość "PdfDocumentStream"
        [ObservableProperty]
        private Stream? _pdfDocumentStream;

        [ObservableProperty]
        private string _newItemUrl = "https://www.dynamicpdf.com/";

        [ObservableProperty]
        private string _newItemDisplayText = "Nowy Element";

        [ObservableProperty]
        private bool _isTocAtStart = true;

        [ObservableProperty]
        private bool _generateQrCodeTable = true;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GeneratePdfCommand))]
        private bool _isGenerating = false;

        public MainViewModel()
        {
            var root1 = new TocItem { DisplayText = "Rozdział 1: Biblioteki .NET", Url = "https://dotnet.microsoft.com/" };
            var child1_1 = new TocItem { DisplayText = "1.1. DynamicPDF", Url = "https://www.dynamicpdf.com/", Parent = root1 };
            var child1_2 = new TocItem { DisplayText = "1.2. Syncfusion", Url = "https://www.syncfusion.com/", Parent = root1 };
            root1.Children.Add(child1_1);
            root1.Children.Add(child1_2);
            TocItems.Add(root1);
        }

        // Atrybut [RelayCommand] generuje publiczną właściwość "AddItemCommand"
        [RelayCommand]
        private void AddItem()
        {
            if (!string.IsNullOrWhiteSpace(NewItemUrl) && !string.IsNullOrWhiteSpace(NewItemDisplayText))
            {
                TocItems.Add(new TocItem { Url = NewItemUrl, DisplayText = NewItemDisplayText });
                NewItemUrl = string.Empty;
                NewItemDisplayText = string.Empty;
            }
        }

        // Metoda pomocnicza dla komendy asynchronicznej
        private bool CanGeneratePdf() => !IsGenerating;

        // Atrybut [RelayCommand] generuje "GeneratePdfCommand" jako AsyncRelayCommand
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
                PdfDocumentStream = await pdfService.GeneratePdfAsync(TocItems, IsTocAtStart, GenerateQrCodeTable);
                MessageBox.Show("Dokument PDF został wygenerowany pomyślnie.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
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
                targetItem.Children.Add(sourceItem);
                sourceItem.Parent = targetItem;
            }
            else
            {
                int insertIndex = dropInfo.InsertIndex;
                if (insertIndex < 0) insertIndex = 0;
                if (insertIndex > TocItems.Count) insertIndex = TocItems.Count;

                TocItems.Insert(insertIndex, sourceItem);
                sourceItem.Parent = null;
            }
        }

        // Oznaczenie jako 'static' jest dobrą praktyką, gdy metoda nie używa stanu instancji.
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
    