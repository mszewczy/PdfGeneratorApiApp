using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace PdfGeneratorApiApp.Models
{
    public partial class TocItem : ObservableObject
    {
        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private string _displayText = string.Empty;

        [ObservableProperty]
        private bool _isExpanded = true;

        // POPRAWKA: Usunięto dodatkowy średnik na końcu linii.
        public ObservableCollection<TocItem> Children { get; set; } = new();

        [JsonIgnore]
        public TocItem? Parent { get; set; }
    }
}
