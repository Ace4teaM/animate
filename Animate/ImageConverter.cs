using System;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class ImageConverter
{
    public static Bitmap ToBitmap(this BitmapImage bitmapImage)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            // Encoder l'image en format PNG ou autre
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
            encoder.Save(stream);

            // Revenir au début du stream
            stream.Position = 0;

            // Créer un Bitmap à partir du stream
            return new Bitmap(stream);
        }
    }
    public static Bitmap ToBitmap(this BitmapSource bitmapImage)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            // Encoder l'image en format PNG ou autre
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
            encoder.Save(stream);
            
            // Revenir au début du stream
            stream.Position = 0;

            // Créer un Bitmap à partir du stream
            return new Bitmap(stream);
        }
    }
    public static bool HasAlpha(this BitmapImage bitmapImage)
    {
        // Vérifie si le format contient un canal alpha
        var format = bitmapImage.Format;

        return format == PixelFormats.Bgra32 ||
               format == PixelFormats.Pbgra32 ||
               format == PixelFormats.Prgba64 ||
               format == PixelFormats.Rgba128Float ||
               format == PixelFormats.Rgba64;
    }
    public static bool HasActualTransparency(this BitmapImage bitmapImage)
    {
        // S'assurer que l'image est dans un format avec alpha
        if (!HasAlpha(bitmapImage))
            return false;

        int width = bitmapImage.PixelWidth;
        int height = bitmapImage.PixelHeight;
        int stride = width * 4; // Bgra32 = 4 octets par pixel

        byte[] pixels = new byte[height * stride];
        bitmapImage.CopyPixels(pixels, stride, 0);

        // Parcourir tous les pixels (4 octets : B, G, R, A)
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte alpha = pixels[i + 3]; // Alpha est le 4ème octet
            if (alpha < 255)
                return true; // Transparence détectée
        }

        return false; // Pas de transparence
    }
}
