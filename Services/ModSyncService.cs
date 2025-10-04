using Google.Apis.Drive.v3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AppMinecraftModManager.Services
{
    internal class ModSyncService
    {
        private readonly DriveService _service;

        public ModSyncService(DriveService service)
        {
            _service = service;
        }

        /// <summary>
        /// Sincroniza los mods desde la carpeta de Google Drive hacia .minecraft/mods
        /// </summary>
        public async Task SyncModsAsync(string driveFolderId, IProgress<(int percent, string fileName)> progress = null)
        {
            string modsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft", "mods");

            Directory.CreateDirectory(modsFolder);

            var request = _service.Files.List();
            request.Q = $"'{driveFolderId}' in parents and mimeType='application/java-archive'";
            request.Fields = "files(id, name, md5Checksum)";
            var driveFiles = await request.ExecuteAsync();

            int total = driveFiles.Files.Count;
            int current = 0;

            foreach (var file in driveFiles.Files)
            {
                current++;
                string localPath = Path.Combine(modsFolder, file.Name);
                bool needsDownload = true;

                if (File.Exists(localPath))
                {
                    string localHash = GetMD5(localPath);
                    if (!string.IsNullOrEmpty(file.Md5Checksum) && file.Md5Checksum == localHash)
                        needsDownload = false;
                }

                if (needsDownload)
                {
                    using (var stream = new FileStream(localPath, FileMode.Create, FileAccess.Write))
                    {
                        await _service.Files.Get(file.Id).DownloadAsync(stream);
                    }
                }

                int percent = (int)((current / (double)total) * 100);
                progress?.Report((percent, file.Name));
            }

            var driveFileNames = driveFiles.Files.Select(f => f.Name).ToHashSet();
            foreach (var localFile in Directory.GetFiles(modsFolder, "*.jar"))
            {
                string fileName = Path.GetFileName(localFile);
                if (!driveFileNames.Contains(fileName))
                    File.Delete(localFile);
            }
        }


        /// <summary>
        /// Calcula el MD5 de un archivo local
        /// </summary>
        private static string GetMD5(string filename)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filename);
            return System.BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
