using DynamicPDF.Api;
using PdfGeneratorApiApp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PdfGeneratorApiApp.Services
{
    public class DynamicPdfApiService
    {
        private static string? _apiKey;

        public static void Initialize(string apiKey)
        {
            _apiKey = apiKey;
            Pdf.DefaultApiKey = apiKey;
        }

        public async Task<MemoryStream?> GeneratePdfAsync(ObservableCollection<TocItem> tocItems, bool isTocAtStart, bool generateQrCodeTable)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Klucz API dla DynamicPDF nie został zainicjalizowany.");
            }

            var pdf = new Pdf();

            // Mapa do przechowywania powiązania między modelem a jego wejściem PDF, kluczowe dla tworzenia zakładek.
            var itemToInputMap = new Dictionary<TocItem, Input>();

            // Faza 1: Utwórz wszystkie strony z treścią jako PageInput i dodaj je do PDF.
            var flatList = Flatten(tocItems).ToList();
            foreach (var item in flatList)
            {
                var pageInput = pdf.AddPage();
                string content = $"To jest treść strony dla: '{item.DisplayText}'.\n\nURL: {item.Url}";
                pageInput.Elements.Add(new TextElement(content, ElementPlacement.TopLeft, 54, 54));
                itemToInputMap[item] = pageInput; // Zapisz powiązanie.
            }

            // Faza 2: Rekurencyjnie zbuduj hierarchię zakładek (Outlines), używając mapy.
            AddOutlinesRecursively(pdf.Outlines, tocItems, itemToInputMap);

            // Faza 3: (Opcjonalnie) Przygotuj i dodaj stronę z tabelą kodów QR na podstawie szablonu DLEX.
            if (generateQrCodeTable)
            {
                var layoutData = CreateQrCodeLayoutData(tocItems);
                // POPRAWKA BŁĘDU CS0019: Poprawne sprawdzanie wartości null.
                if (layoutData is not null)
                {
                    // POPRAWKA BŁĘDU CS1503: Użycie DlexResource i poprawnego konstruktora DlexInput.
                    var dlexResource = new DlexResource("Resources/qr-code-template.dlex");
                    var dlexInput = new DlexInput(dlexResource, layoutData);

                    if (isTocAtStart)
                    {
                        // Wstawia DLEX na początku listy instrukcji.
                        pdf.Inputs.Insert(0, dlexInput);
                    }
                    else
                    {
                        pdf.Inputs.Add(dlexInput);
                    }
                }
            }

            // Faza 4: Wyślij instrukcje do API i przetwórz odpowiedź.
            var response = await pdf.ProcessAsync();

            if (response.IsSuccessful)
            {
                return new MemoryStream(response.Content);
            }
            else
            {
                Console.WriteLine(response.ErrorJson);
                throw new Exception($"Błąd API DynamicPDF: {response.ErrorId} - {response.ErrorMessage}");
            }
        }

        // Metoda do rekurencyjnego tworzenia zakładek zgodnie z nowym API.
        private void AddOutlinesRecursively(OutlineList parentOutlines, IEnumerable<TocItem> items, IReadOnlyDictionary<TocItem, Input> itemToInputMap)
        {
            foreach (var item in items)
            {
                var targetInput = itemToInputMap[item];

                // POPRAWKA BŁĘDÓW CS1729, CS0117, CS1503:
                // 1. Użycie konstruktora Outline z tekstem.
                // 2. Ustawienie właściwości Action na obiekt GoToAction, który wskazuje na Input, a nie numer strony.
                var outline = new Outline(item.DisplayText)
                {
                    Action = new GoToAction(targetInput)
                };
                parentOutlines.Add(outline);

                // POPRAWKA OSTRZEŻENIA CA1860: Użycie .Count > 0 zamiast .Any() dla kolekcji.
                if (item.Children.Count > 0)
                {
                    AddOutlinesRecursively(outline.Children, item.Children, itemToInputMap);
                }
            }
        }

        // POPRAWKA BŁĘDU CS0246: Poprawny typ zwracany to LayoutData?
        private LayoutData? CreateQrCodeLayoutData(IEnumerable<TocItem> tocItems)
        {
            var urls = Flatten(tocItems)
                .Where(item => !string.IsNullOrWhiteSpace(item.Url))
                .Select(item => new { description = item.DisplayText, url = item.Url })
                .Distinct()
                .ToList();

            if (urls.Count == 0) return null;

            var layoutData = new LayoutData();
            layoutData.Add("QrCodeData", urls);
            return layoutData;
        }

        private static IEnumerable<TocItem> Flatten(IEnumerable<TocItem> items)
        {
            var stack = new Stack<TocItem>(items.Reverse());
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;
                foreach (var child in current.Children.Reverse())
                {
                    stack.Push(child);
                }
            }
        }
    }
}
