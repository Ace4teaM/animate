using Microsoft.Win32;
using System.IO;
using System.Text.Json;

namespace Animate
{
    /// <summary>
    /// Fonctions utiles pour gérer l'éditeur de registre
    /// </summary>
    public static class RegistryManager
    {
        /// <summary>
        /// Emplacement de la clé de registre pour stocker les paramètres d'image
        /// </summary>
        private const string REGISTRY_KEY_PATH = @"SOFTWARE\Animate\ImageSettings";

        /// <summary>
        /// Sauvegarde les paramètres d'image dans le registre
        /// </summary>
        public static void SaveImageSettings(string imagePath, List<Frame> frames, ExportSettings? exportSettings = null)
        {
            if (string.IsNullOrEmpty(imagePath) || frames == null)
                return;

            try
            {
                string keyName = GenerateKeyName(imagePath);
                var imageSettings = new ImageSettings
                {
                    Frames = frames.Select(f => new FrameData
                    {
                        X = f.rect.X,
                        Y = f.rect.Y,
                        Width = f.rect.Width,
                        Height = f.rect.Height,
                        OriginX = f.origin.X,
                        OriginY = f.origin.Y,
                        FrameCount = f.frameCount
                    }).ToList(),
                    ExportSettings = exportSettings
                };

                string jsonData = JsonSerializer.Serialize(imageSettings, new JsonSerializerOptions 
                { 
                    WriteIndented = false 
                });

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY_PATH))
                {
                    key.SetValue(keyName, jsonData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la sauvegarde des paramètres: {ex.Message}");
            }
        }

        /// <summary>
        /// Charge les paramètres d'image depuis le registre
        /// </summary>
        public static ImageSettings? LoadImageSettings(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;

            try
            {
                string keyName = GenerateKeyName(imagePath);

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH))
                {
                    if (key == null)
                        return null;

                    string? jsonData = key.GetValue(keyName) as string;
                    if (string.IsNullOrEmpty(jsonData))
                        return null;

                    return JsonSerializer.Deserialize<ImageSettings>(jsonData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement des paramètres: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Supprime les paramètres d'image du registre
        /// </summary>
        public static void DeleteImageSettings(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return;

            try
            {
                string keyName = GenerateKeyName(imagePath);

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, true))
                {
                    key?.DeleteValue(keyName, false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la suppression des paramètres: {ex.Message}");
            }
        }

        /// <summary>
        /// Génère un hash du chemin d'accès pour ^tre utilisé comme clé unique dans le registre
        /// </summary>
        private static string GenerateKeyName(string imagePath)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(imagePath)))
                .Replace('+', '_')
                .Replace('/', '-')
                .Replace("=", "");
        }

        /// <summary>
        /// Supprime toutes les données du registre pour l'application Animate
        /// </summary>
        public static void ClearAllSettings()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(REGISTRY_KEY_PATH, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la suppression de toutes les données: {ex.Message}");
            }
        }

        /// <summary>
        /// Supprimer les entrées du registre pour les fichiers manquants
        /// </summary>
        public static void CleanupMissingFiles()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, true))
                {
                    if (key == null) return;

                    var keysToDelete = new List<string>();
                    
                    foreach (string valueName in key.GetValueNames())
                    {
                        try
                        {
                            byte[] data = Convert.FromBase64String(valueName.Replace('_', '+').Replace('-', '/') + "==");
                            string filePath = System.Text.Encoding.UTF8.GetString(data);
                            
                            if (!File.Exists(filePath))
                            {
                                keysToDelete.Add(valueName);
                            }
                        }
                        catch
                        {
                            keysToDelete.Add(valueName);
                        }
                    }

                    foreach (string keyName in keysToDelete)
                    {
                        key.DeleteValue(keyName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du nettoyage: {ex.Message}");
            }
        }
    }

    public class ImageSettings
    {
        public List<FrameData> Frames { get; set; } = new();
        public ExportSettings? ExportSettings { get; set; }
    }

    public class FrameData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float OriginX { get; set; }
        public float OriginY { get; set; }
        public int FrameCount { get; set; }
    }

    public class ExportSettings
    {
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public bool AdjustSize { get; set; }
        public int SliderValue { get; set; }
    }
}