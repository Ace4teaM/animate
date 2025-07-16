using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Animate
{
    public class Frame
    {
        internal Int32Rect rect;
        internal Vector2 origin;
        internal int frameCount = 1;

        public string Position
        {
            get
            {
                return String.Format("{0}, {1} => {2}, {3}", rect.X, rect.Y, rect.Width, rect.Height);
            }
        }
        public string Origin
        {
            get
            {
                return String.Format("{0}, {1}", origin.X, origin.Y);
            }
        }
        public int FrameCount
        {
            get
            {
                return frameCount;
            }
        }

        public override string ToString()
        {
            return Position;
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private BitmapImage spriteSheet;
        private bool spriteSheetTransparency;
        FileSystemWatcher? spriteSheetSystemWatcher;
        public ObservableCollection<Frame> Frames { get; } = new ObservableCollection<Frame>();
        private System.Windows.Point startPoint;
        private System.Windows.Shapes.Rectangle selectionRect;
        private bool isDrawing = false;

        private int currentFrameIndex = 0;
        private DispatcherTimer animationTimer;

        public event PropertyChangedEventHandler? PropertyChanged;


        protected void OnPropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string FrameDuration
        {
            get
            {
                return animationTimer.Interval.TotalMilliseconds.ToString(@"0ms");
            }
            set
            {
                double dinterval = 0;
                if (value.Contains(".") && double.TryParse(value, out dinterval) && dinterval >= 100 && dinterval <= 1)
                {
                    animationTimer.Interval = TimeSpan.FromSeconds(dinterval);
                    OnPropertyChange(nameof(FrameDurationValue));
                    return;
                }

                int interval = 0;
                if (int.TryParse(value, out interval) && interval >= 100 && interval <= 1000)
                {
                    animationTimer.Interval = TimeSpan.FromMilliseconds(interval);
                    OnPropertyChange(nameof(FrameDurationValue));
                    return;
                }
            }
        }

        public double FrameDurationValue
        {
            get
            {
                return animationTimer.Interval.TotalMilliseconds;
            }
            set
            {
                animationTimer.Interval = TimeSpan.FromMilliseconds(value);
                OnPropertyChange(nameof(FrameDuration));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            SetupAnimationTimer();
        }

        /// <summary>
        /// Ajuste l'origine du rectangle au centre de masse du dessin
        /// </summary>
        internal void AdjustOrigins(IEnumerable<Frame> frames)
        {
            // 1. Charger l’image
            using Mat src = spriteSheet.ToBitmap().ToMat();

            foreach (var frame in frames)
            {
                // 1. Définir la zone d’intérêt (ROI)
                using Mat img = new Mat(src, new System.Drawing.Rectangle(frame.rect.X, frame.rect.Y, frame.rect.Width, frame.rect.Height));

                // 2. Extraire le canal alpha si transparent (RGBA)
                Mat alpha = new Mat();
                if (img.NumberOfChannels == 4)
                {
                    VectorOfMat channels = new VectorOfMat();
                    CvInvoke.Split(img, channels);
                    alpha = channels[3]; // Canal alpha
                }
                else
                {
                    // Si pas de transparence, créer un masque par couleur (ex: fond blanc)
                    using Mat mask = new Mat();
                    CvInvoke.InRange(img, new ScalarArray(new MCvScalar(250, 250, 250)), new ScalarArray(new MCvScalar(255, 255, 255)), mask);
                    CvInvoke.BitwiseNot(mask, alpha); // Inverser pour avoir l'objet
                }

                // 3. Trouver les moments de l’objet
                using Moments m = CvInvoke.Moments(alpha, true);

                if (m.M00 != 0)
                {
                    int cx = (int)(m.M10 / m.M00);
                    int cy = (int)(m.M01 / m.M00);
                    Console.WriteLine($"Centre du dessin (en ignorant le fond) : ({cx}, {cy})");

                    frame.origin = new Vector2(cx, cy);
                }
                else
                {
                    Console.WriteLine("Aucune forme détectée !");
                }

                alpha.Dispose();
            }
        }

        /// <summary>
        /// Ajuste la taille du rectangle au contenu du dessin
        /// </summary>
        internal void AdjustBoundFromSolidBackground(IEnumerable<Frame> frames)
        {
            // 1. Charger l’image
            using Mat src = spriteSheet.ToBitmap().ToMat();

            foreach (var frame in frames)
            {
                // 1. Définir la zone d’intérêt (ROI)
                using Mat img = new Mat(src, new System.Drawing.Rectangle(frame.rect.X, frame.rect.Y, frame.rect.Width, frame.rect.Height));

                // 2. Convertir en HSV (plus stable pour détecter une couleur unie)
                Mat imgHsv = new Mat();
                CvInvoke.CvtColor(img, imgHsv, ColorConversion.Bgr2Hsv);

                // 3. Définir la couleur du fond (ex: coin supérieur gauche)
                var bgColor = imgHsv.ToImage<Hsv, byte>()[0, 0];//utiliser un échantillon ?

                // 4. Définir une tolérance autour de la couleur du fond
                Hsv lower = new Hsv(bgColor.Hue - 10, Math.Max(bgColor.Satuation - 40, 0), Math.Max(bgColor.Value - 40, 0));
                Hsv upper = new Hsv(bgColor.Hue + 10, Math.Min(bgColor.Satuation + 40, 255), Math.Min(bgColor.Value + 40, 255));

                // 5. Créer un masque du fond
                Image<Hsv, byte> imgHsvImage = imgHsv.ToImage<Hsv, byte>();
                Image<Gray, byte> mask = imgHsvImage.InRange(lower, upper);

                // 6. Inverser le masque (car on veut l’objet)
                mask = mask.Not();

                // 7. Nettoyer le masque (morphologie)
                mask = mask.SmoothGaussian(5);
                CvInvoke.Erode(mask, mask, null, new System.Drawing.Point(-1, -1), 1, BorderType.Default, new MCvScalar());
                CvInvoke.Dilate(mask, mask, null, new System.Drawing.Point(-1, -1), 2, BorderType.Default, new MCvScalar());

                VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
                CvInvoke.FindContours(mask, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

                // Calcul de la bounding box globale
                System.Drawing.Rectangle? globalBoundingBox = null;

                for (int i = 0; i < contours.Size; i++)
                {
                    var box = CvInvoke.BoundingRectangle(contours[i]);

                    // Étendre le rectangle global
                    globalBoundingBox = globalBoundingBox.HasValue
                        ? System.Drawing.Rectangle.Union(globalBoundingBox.Value, box)
                        : box;
                }

                // Applique la nouvelle zone à la frame
                if (globalBoundingBox.HasValue)
                {
                    frame.rect = new Int32Rect(frame.rect.X + globalBoundingBox.Value.X, frame.rect.Y + globalBoundingBox.Value.Y, globalBoundingBox.Value.Width, globalBoundingBox.Value.Height);
                    frame.origin = new Vector2(frame.rect.Width / 2.0f, frame.rect.Height / 2.0f);
                }
            }
        }

        /// <summary>
        /// Ajuste la taille du rectangle au contenu du dessin
        /// </summary>
        internal void AdjustBound(IEnumerable<Frame> frames)
        {
            // 1. Charger l’image
            using Mat src = spriteSheet.ToBitmap().ToMat();

            foreach (var frame in frames)
            {
                // 1. Définir la zone d’intérêt (ROI)
                using Mat img = new Mat(src, new System.Drawing.Rectangle(frame.rect.X, frame.rect.Y, frame.rect.Width, frame.rect.Height));

                // Convertir en niveaux de gris
                Mat gray = new Mat();
                CvInvoke.CvtColor(img, gray, ColorConversion.Bgr2Gray);

                // Appliquer un seuillage automatique
                Mat thresh = new Mat();
                CvInvoke.Threshold(gray, thresh, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);

                // Trouver les contours dans le ROI
                VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
                CvInvoke.FindContours(thresh, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

                // Calcul de la bounding box globale
                System.Drawing.Rectangle? globalBoundingBox = null;

                for (int i = 0; i < contours.Size; i++)
                {
                    var box = CvInvoke.BoundingRectangle(contours[i]);

                    // Étendre le rectangle global
                    globalBoundingBox = globalBoundingBox.HasValue
                        ? System.Drawing.Rectangle.Union(globalBoundingBox.Value, box)
                        : box;
                }

                // Applique la nouvelle zone à la frame
                if (globalBoundingBox.HasValue)
                {
                    frame.rect = new Int32Rect(frame.rect.X + globalBoundingBox.Value.X, frame.rect.Y + globalBoundingBox.Value.Y, globalBoundingBox.Value.Width, globalBoundingBox.Value.Height);
                    frame.origin = new Vector2(frame.rect.Width / 2.0f, frame.rect.Height / 2.0f);
                }
            }
        }

        internal void ShowOrigins(IEnumerable<Frame> frames)
        {
            HideOrigins(frames);

            foreach (var frame in frames)
            {
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 2 },
                    Width = frame.rect.Width,
                    Height = frame.rect.Height,
                    Tag = frame
                };
                Canvas.SetLeft(rect, frame.rect.X);
                Canvas.SetTop(rect, frame.rect.Y);
                ImageCanvas.Children.Add(rect);

                var origin = new System.Windows.Shapes.Ellipse
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 2 },
                    Width = 7,
                    Height = 7,
                    Margin = new Thickness(-3, -3, 0, 0),
                    Tag = frame
                };
                Canvas.SetLeft(origin, frame.rect.X + frame.origin.X);
                Canvas.SetTop(origin, frame.rect.Y + frame.origin.Y);
                ImageCanvas.Children.Add(origin);
            }
        }
        /*
        public static Image<Bgra, byte> RemoveBackgroundByColor(Image<Bgr, byte> img, Bgr backgroundColor, double tolerance)
        {
            // Convertir en BGRA (pour gérer la transparence)
            Image<Bgra, byte> imgWithAlpha = img.Convert<Bgra, byte>();

            for (int y = 0; y < imgWithAlpha.Height; y++)
            {
                for (int x = 0; x < imgWithAlpha.Width; x++)
                {
                    Bgra pixel = imgWithAlpha[y, x];
                    // Calculer la distance couleur euclidienne
                    double dist = Math.Sqrt(
                        Math.Pow(pixel.Blue - backgroundColor.Blue, 2) +
                        Math.Pow(pixel.Green - backgroundColor.Green, 2) +
                        Math.Pow(pixel.Red - backgroundColor.Red, 2)
                    );

                    if (dist <= tolerance)
                    {
                        // Couleur proche du fond => transparent
                        pixel.Alpha = 0;
                    }
                    else
                    {
                        // Conserver pixel opaque
                        pixel.Alpha = 255;
                    }
                    imgWithAlpha[y, x] = pixel;
                }
            }

            return imgWithAlpha;
        }
        */
        internal void HideOrigins(IEnumerable<Frame> frames)
        {
            foreach (var frame in frames)
            {
                foreach (var child in ImageCanvas.Children.OfType<FrameworkElement>().Where(p => p.Tag == frame).ToArray())
                {
                    ImageCanvas.Children.Remove(child);
                }
            }
        }

        private void SetupAnimationTimer()
        {
            animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            animationTimer.Tick += (s, e) =>
            {
                if (Frames.Count == 0 || spriteSheet == null) return;

                var frame = Frames[currentFrameIndex];
                var cropped = new CroppedBitmap(spriteSheet, frame.rect);
                AnimedImage.Source = cropped;

                currentFrameIndex = (currentFrameIndex + 1) % Frames.Count;
            };
            animationTimer.Start();
            OnPropertyChange(nameof(FrameDuration));
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Image files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp";

            if (dlg.ShowDialog() == true)
            {
                LoadImage(dlg.FileName);
            }
        }


        internal void LoadImage(string path)
        {
            spriteSheet = new BitmapImage(new Uri(path));
            spriteSheetTransparency = spriteSheet.HasActualTransparency();

            MainImage.Source = spriteSheet;
            MainImage.Width = spriteSheet.PixelWidth;
            MainImage.Height = spriteSheet.PixelHeight;
            ImageCanvas.Width = spriteSheet.PixelWidth;
            ImageCanvas.Height = spriteSheet.PixelHeight;

            ClearFrames(); // Optionnel : garde ou efface les frames existants

            /*spriteSheetSystemWatcher = new FileSystemWatcher(path);
            spriteSheetSystemWatcher.NotifyFilter = NotifyFilters.LastWrite;
            spriteSheetSystemWatcher.Changed += SpriteSheetSystemWatcher_Changed;*/
        }

        private void SpriteSheetSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (spriteSheet == null) return;

            isDrawing = true;
            startPoint = e.GetPosition(ImageCanvas);

            selectionRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 2 },
                Width = 0,
                Height = 0
            };
            Canvas.SetLeft(selectionRect, startPoint.X);
            Canvas.SetTop(selectionRect, startPoint.Y);
            ImageCanvas.Children.Add(selectionRect);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDrawing || selectionRect == null) return;

            Point pos = e.GetPosition(ImageCanvas);

            double x = Math.Min(pos.X, startPoint.X);
            double y = Math.Min(pos.Y, startPoint.Y);
            double w = Math.Abs(pos.X - startPoint.X);
            double h = Math.Abs(pos.Y - startPoint.Y);

            selectionRect.Width = w;
            selectionRect.Height = h;
            Canvas.SetLeft(selectionRect, x);
            Canvas.SetTop(selectionRect, y);
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDrawing) return;

            isDrawing = false;
            Point endPoint = e.GetPosition(ImageCanvas);

            int x = (int)Math.Min(startPoint.X, endPoint.X);
            int y = (int)Math.Min(startPoint.Y, endPoint.Y);
            int w = (int)Math.Abs(endPoint.X - startPoint.X);
            int h = (int)Math.Abs(endPoint.Y - startPoint.Y);

            if (w > 5 && h > 5)
            {
                var r = new Int32Rect(x, y, w, h);
                var frame = new Frame { rect = r, frameCount = 1, origin = new Vector2(r.Width / 2.0f, r.Height / 2.0f) };
                Frames.Add(frame);

                //if (spriteSheetTransparency == false)//pas d'alpha
                    AdjustBoundFromSolidBackground(new[] { frame });
               // else
                //    AdjustBound(new[] { frame });

                ShowOrigins(new[] { frame });
            }

            ImageCanvas.Children.Remove(selectionRect);
        }

        private void ClearFrames_Click(object sender, RoutedEventArgs e)
        {
            ClearFrames();
        }

        private void ClearFrames()
        {
            Frames.Clear();
            currentFrameIndex = 0;

            // Efface tous les rectangles sauf l’image
            foreach (var child in ImageCanvas.Children.OfType<FrameworkElement>().Where(p => p.Tag is Frame).ToArray())
            {
                ImageCanvas.Children.Remove(child);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var wd = Directory.GetCurrentDirectory();
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                var path = Path.GetFullPath(args[1]);
                LoadImage(path);
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = ((ListView)sender).SelectedItem as Frame;
            if (item != null)
            {
            }
        }

        private void ShowOrigins_Click(object sender, RoutedEventArgs e)
        {
            ShowOrigins(Frames);
        }

        private void HideOrigins_Click(object sender, RoutedEventArgs e)
        {
            HideOrigins(Frames);
        }

        private void AdjusteOrigins_Click(object sender, RoutedEventArgs e)
        {
            AdjustOrigins(Frames);
        }

        private void ExportImages_Click(object sender, RoutedEventArgs e)
        {
            ExportImages();
        }

        private void ExportImages()
        {
            if (spriteSheet == null || Frames.Count == 0)
            {
                MessageBox.Show("Aucune image chargée ou aucun sprite défini.");
                return;
            }
            var wnd = new ExportWindow();
            wnd.ImageWidth = Frames.Select(p=>p.rect.Width).Max();
            wnd.ImageHeight = Frames.Select(p=>p.rect.Height).Max();
            wnd.Owner = this;
            wnd.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (wnd.ShowDialog() == true)
            {
                int width = wnd.AdjustSize == false ? wnd.ImageWidth : wnd.AdjustedImageWidth;
                int height = wnd.AdjustSize == false ? wnd.ImageHeight : wnd.AdjustedImageHeight;
                try
                {
                    var folderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    if (Directory.Exists(folderPath))
                        Directory.Delete(folderPath, true);

                    Directory.CreateDirectory(folderPath);

                    for (int i = 0; i < Frames.Count; i++)
                    {
                        var origin = Frames[i].origin;

                        var rect = Frames[i].rect;
                        var cropped = new CroppedBitmap(spriteSheet, rect);

                        var final = new System.Drawing.Bitmap(width, height);

                        using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(final))
                        {
                            g.Clear(System.Drawing.Color.Transparent);
                            g.DrawImage(cropped.ToBitmap(), (width / 2) - origin.X, (height / 2) - origin.Y);
                        }

                        string fileName = Path.Combine(folderPath, $"sprite_{i}.png");

                        final.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
                        /*
                        using (FileStream stream = new FileStream(fileName, FileMode.Create))
                        {
                            PngBitmapEncoder encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(final));
                            encoder.Save(stream);
                        }*/
                    }

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folderPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }
    }
}