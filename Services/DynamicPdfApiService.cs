using DynamicPDF.Api;
using DynamicPDF.Api.Elements;
using PdfGeneratorApiApp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
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

        public async Task<MemoryStream?> GeneratePdfAsync(ObservableCollection<TocItem> tocItems, bool isTocAtStart, bool generateQrCodeTable, bool addToc)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Klucz API dla DynamicPDF nie został zainicjalizowany.");
            }

            var pdf = new Pdf();
            // POPRAWKA: Użycie collection expressions dla .NET 8
            Dictionary<TocItem, Input> itemToInputMap = [];

            // Faza 1: Utwórz wszystkie strony z treścią jako PageInput.
            // POPRAWKA: Użycie collection expressions dla .NET 8
            List<PageInput> pageInputs = [];
            foreach (var item in Flatten(tocItems))
            {
                if (!string.IsNullOrEmpty(item.Url)) // Tylko strony z URL mają zawartość
                {
                    var pageInput = new PageInput();
                    string content = $"To jest treść strony dla: '{item.DisplayText}'.\n\nURL: {item.Url}";
                    pageInput.Elements.Add(new TextElement(content, ElementPlacement.TopLeft, 54, 54));
                    itemToInputMap[item] = pageInput;
                    pageInputs.Add(pageInput);
                }
            }

            // Faza 2: (Opcjonalnie) Przygotuj DlexInput dla tabeli z kodami QR.
            DlexInput? dlexInput = null;
            if (generateQrCodeTable)
            {
                var layoutData = CreateQrCodeLayoutData(tocItems);
                if (layoutData != null)
                {
                    var dlexResource = new DlexResource("Resources/qr-code-template.dlex");
                    // POPRAWKA: Dodano opcje serializacji JSON dla lepszej wydajności i zgodności z .NET 8
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    };
                    var layoutDataResource = new LayoutDataResource(JsonSerializer.Serialize(layoutData, jsonOptions));
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
            if (addToc)
            {
                AddOutlinesRecursively(pdf.Outlines, tocItems, itemToInputMap);
            }


            // Faza 5: Wyślij instrukcje do API i przetwórz odpowiedź.
            // POPRAWKA: Dodano ConfigureAwait(false) dla lepszej wydajności w kontekście biblioteki
            var response = await pdf.ProcessAsync().ConfigureAwait(false);

            if (response.IsSuccessful)
            {
                return new MemoryStream(response.Content);
            }
            else
            {
                Console.WriteLine(response.ErrorJson);
                // POPRAWKA: Użycie bardziej specyficznego typu wyjątku dla błędów HTTP API
                throw new HttpRequestException($"Błąd API DynamicPDF: {response.ErrorId} - {response.ErrorMessage}");
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
                else
                {
                    // Dodaj zakładkę bez powiązanej akcji (np. dla folderów)
                    var outline = parentOutlines.Add(item.DisplayText);
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