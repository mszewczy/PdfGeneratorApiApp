using DynamicPDF.Api;
using PdfGeneratorApiApp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
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
            var itemToInputMap = new Dictionary<TocItem, Input>();

            // Faza 1: Utwórz wszystkie strony z treścią jako PageInput.
            var pageInputs = new List<PageInput>();
            foreach (var item in Flatten(tocItems))
            {
                var pageInput = new PageInput();
                string content = $"To jest treść strony dla: '{item.DisplayText}'.\n\nURL: {item.Url}";

                // POPRAWKA: Użycie pełnych nazw typów w celu rozwiązania problemów z ich odnalezieniem.
                pageInput.Elements.Add(new DynamicPDF.Api.TextElement(content, DynamicPDF.Api.ElementPlacement.TopLeft, 54, 54));
                itemToInputMap[item] = pageInput;
                pageInputs.Add(pageInput);
            }

            // Faza 2: (Opcjonalnie) Przygotuj DlexInput dla tabeli z kodami QR.
            DlexInput? dlexInput = null;
            if (generateQrCodeTable)
            {
                var layoutData = CreateQrCodeLayoutData(tocItems);
                if (layoutData != null)
                {
                    var dlexResource = new DlexResource("Resources/qr-code-template.dlex");
                    var layoutDataResource = new LayoutDataResource(JsonSerializer.Serialize(layoutData));
                    dlexInput = new DlexInput(dlexResource, layoutDataResource);
                }
            }

            // Faza 3: Złóż dokument w odpowiedniej kolejności.
            if (isTocAtStart && dlexInput != null)
            {
                pdf.Inputs.Add(dlexInput);
            }
            foreach (var page in pageInputs)
            {
                pdf.Inputs.Add(page);
            }
            if (!isTocAtStart && dlexInput != null)
            {
                pdf.Inputs.Add(dlexInput);
            }

            // Faza 4: Rekurencyjnie zbuduj hierarchię zakładek (Outlines).
            AddOutlinesRecursively(pdf.Outlines, tocItems, itemToInputMap);

            // Faza 5: Wyślij instrukcje do API i przetwórz odpowiedź.
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

        private void AddOutlinesRecursively(OutlineList parentOutlines, IEnumerable<TocItem> items, IReadOnlyDictionary<TocItem, Input> itemToInputMap)
        {
            foreach (var item in items)
            {
                if (itemToInputMap.TryGetValue(item, out var targetInput))
                {
                    var outline = parentOutlines.Add(item.DisplayText, targetInput);

                    if (item.Children.Any())
                    {
                        AddOutlinesRecursively(outline.Children, item.Children, itemToInputMap);
                    }
                }
            }
        }

        private object? CreateQrCodeLayoutData(IEnumerable<TocItem> tocItems)
        {
            var urls = Flatten(tocItems)
                .Where(item => !string.IsNullOrWhiteSpace(item.Url))
                .Select(item => new { description = item.DisplayText, url = item.Url })
                .Distinct()
                .ToList();

            if (!urls.Any()) return null;

            return new { QrCodeData = urls };
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