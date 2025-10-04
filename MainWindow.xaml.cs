using AppMinecraftModManager.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;
using AppWindow = Microsoft.UI.Windowing.AppWindow;
using Microsoft.UI.Xaml.Media.Imaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AppMinecraftModManager
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private MicaBackdrop micaBackdrop;
        private AppWindow appWindow;

        public MainWindow()
        {
            InitializeComponent();

            // Obtener AppWindow
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);

            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(400, 400));
            TrySetMicaBackdrop();
            ExtenderTituloWin11();

            // Enchanting table GIF
            var gifPath = "Assets/Enchanting_Table.gif"; // ruta relativa en tu proyecto
            var uri = new Uri($"ms-appx:///{gifPath}");

            var bitmap = new BitmapImage(uri);
            AnimatedGif.Source = bitmap;
        }
        private void TrySetMicaBackdrop()
        {
            micaBackdrop = new MicaBackdrop();

            if (micaBackdrop != null && this.SystemBackdrop == null)
            {
                this.SystemBackdrop = micaBackdrop;
            }
        }
        private void ExtenderTituloWin11()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            appWindow = AppWindow.GetFromWindowId(windowId);
            // Extiende el contenido al área de título
            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;

            // Hacemos botones (minimizar, cerrar, etc.) transparentes para que combinen con Mica
            titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        }
        class GoogleDriveHelper
        {
            public static DriveService GetDriveService()
            {
                // El archivo JSON que descargaste de la Service Account
                string serviceAccountPath = Path.Combine(AppContext.BaseDirectory, "minecraft-mods-manager-50dded4b61e3.json");

                GoogleCredential credential;
                using (var stream = new FileStream(serviceAccountPath, FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(DriveService.Scope.DriveReadonly);
                }

                // Crear el servicio de Drive autenticado
                var service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Minecraft Mod Updater Service Account",
                });

                return service;
            }
        }
        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            // Bloquear UI
            SyncButton.IsEnabled = false;
            ProgressBarMods.Visibility = Visibility.Visible;
            ProgressBarMods.Value = 0;
            ProgressText.Text = "Iniciando sincronización...";

            var driveService = GoogleDriveHelper.GetDriveService();
            var modSync = new ModSyncService(driveService);

            // Progress report: (percent, fileName)
            var progress = new Progress<(int percent, string fileName)>(report =>
            {
                ProgressBarMods.Value = report.percent;
                ProgressText.Text = $"{report.fileName} — {report.percent}%";
            });

            try
            {
                await modSync.SyncModsAsync("1QEGN5ugrqB_YHrw4rh37sKFXwdst7HL3", progress);

                ProgressText.Text = "Mods sincronizados correctamente.";
            }
            catch (Exception ex)
            {
                ProgressText.Text = "Error: " + ex.Message;
            }
            finally
            {
                ProgressBarMods.Visibility = Visibility.Collapsed;
                SyncButton.IsEnabled = true;
            }
        }
        public async Task DescargarMods(string carpetaIdDrive)
        {
            var service = GoogleDriveHelper.GetDriveService();

            // Carpeta de mods en el cliente
            string modsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Roaming",".minecraft", "mods");

            Directory.CreateDirectory(modsFolder);

            // Listar archivos en la carpeta de Drive
            var request = service.Files.List();
            request.Q = $"'{carpetaIdDrive}' in parents and mimeType='application/java-archive'";
            var files = await request.ExecuteAsync();

            foreach (var file in files.Files)
            {
                string localPath = Path.Combine(modsFolder, file.Name);

                // Si ya existe, lo saltamos
                if (File.Exists(localPath)) continue;

                using (var stream = new FileStream(localPath, FileMode.Create, FileAccess.Write))
                {
                    await service.Files.Get(file.Id).DownloadAsync(stream);
                }
            }
        }
    }
}
