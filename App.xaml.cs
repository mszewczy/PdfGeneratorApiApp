using Microsoft.Extensions.Configuration;
using PdfGeneratorApiApp.Services;
using System.IO;
using System.Windows;

namespace PdfGeneratorApiApp
{
    public partial class App : Application
    {
        public static IConfiguration? Configuration { get; private set; }

        public App()
        {
            // POPRAWKA: Użycie bardziej nowoczesnego wzorca konfiguracji dla .NET 8.0
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables(); // Dodanie wsparcia dla zmiennych środowiskowych zgodnie z .NET 8.0 best practices

            Configuration = builder.Build();

            // POPRAWKA: Prawidłowy sposób odczytywania konkretnego klucza z konfiguracji.
            var syncfusionLicenseKey = Configuration["SyncfusionLicense"];
            if (!string.IsNullOrEmpty(syncfusionLicenseKey) && !syncfusionLicenseKey.Contains("WSTAW"))
            {
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
            }
            else
            {
                MessageBox.Show("Klucz licencyjny Syncfusion nie został znaleziony lub jest nieprawidłowy w pliku appsettings.json.", "Błąd Konfiguracji", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // POPRAWKA: Prawidłowy sposób odczytywania konkretnego klucza z konfiguracji.
            var dynamicPdfApiKey = Configuration["DynamicPDFApiKey"];
            if (!string.IsNullOrEmpty(dynamicPdfApiKey) && !dynamicPdfApiKey.Contains("WSTAW"))
            {
                DynamicPdfApiService.Initialize(dynamicPdfApiKey);
            }
            else
            {
                MessageBox.Show("Klucz API dla DynamicPDF nie został znaleziony lub jest nieprawidłowy w pliku appsettings.json.", "Błąd Konfiguracji", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
