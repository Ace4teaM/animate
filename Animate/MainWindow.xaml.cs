using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Cache;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

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
        private BitmapImage? spriteSheet;
        private bool spriteSheetTransparency = false;
        private bool spriteChanged = false;
        private string? spritePath;
        FileSystemWatcher? spriteSheetSystemWatcher;
        public ObservableCollection<Frame> Frames { get; } = [];
        private System.Windows.Point startPoint;
        private System.Windows.Shapes.Rectangle? selectionRect;
        private bool isDrawing = false;
        private bool isPanning = false;

        private int currentFrameIndex = 0;
        private DispatcherTimer? animationTimer;
        private DispatcherTimer? reloadTimer;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string FrameDuration
        {
            get
            {
                return (animationTimer != null) ? animationTimer.Interval.TotalMilliseconds.ToString(@"0ms") : String.Empty;
            }
            set
            {
                if (animationTimer != null)
                {
                    if (value.Contains('.') && double.TryParse(value, out double dinterval) && dinterval >= 100 && dinterval <= 1)
                    {
                        animationTimer.Interval = TimeSpan.FromSeconds(dinterval);
                        OnPropertyChange(nameof(FrameDurationValue));
                        return;
                    }

                    if (int.TryParse(value, out int interval) && interval >= 100 && interval <= 1000)
                    {
                        animationTimer.Interval = TimeSpan.FromMilliseconds(interval);
                        OnPropertyChange(nameof(FrameDurationValue));
                        return;
                    }
                }
            }
        }

        public static Version? CurrentVersion
        {
            get
            {
                return Assembly.GetEntryAssembly()?.GetName().Version;
            }
        }

        public double FrameDurationValue
        {
            get
            {
                return (animationTimer != null) ? animationTimer.Interval.TotalMilliseconds : 0.0;
            }
            set
            {
                if (animationTimer != null)
                {
                    animationTimer.Interval = TimeSpan.FromMilliseconds(value);
                    OnPropertyChange(nameof(FrameDuration));
                }
            }
        }

        public Frame? SelectedFrame
        {
            get
            {
                if (Frames.Count == 0 || spriteSheet == null) return null;

                return Frames[currentFrameIndex];
            }
            set
            {
                if (value == null)
                    currentFrameIndex = 0;
                else
                    currentFrameIndex = Frames.IndexOf(value);

                SetFrame(currentFrameIndex);
            }
        }

        internal void SetFrame(int frameIndex)
        {
            if (frameIndex >= Frames.Count || frameIndex < 0 || spriteSheet == null) return;

            currentFrameIndex = frameIndex;

            var frame = Frames[currentFrameIndex];
            var cropped = new CroppedBitmap(spriteSheet, frame.rect);
            AnimedImage.Source = cropped;

            OnPropertyChange(nameof(PlaySymbol));

            OnPropertyChange(nameof(SelectedFrame));
        }

        public MainWindow()
        {
            InitializeComponent();
            SetupAnimationTimer();
            SetupReloadTimer();
            this.DataContext = this;
        }

        /// <summary>
        /// Ajuste l'origine du rectangle au centre de masse du dessin
        /// </summary>
        internal void AdjustOrigins(IEnumerable<Frame> frames)
        {
            if (spriteSheet == null)
                return;

            // 1. Charger l’image
            using Mat src = spriteSheet.ToBitmap().ToMat();

            foreach (var frame in frames)
            {
                // 1. Définir la zone d’intérêt (ROI)
                using var img = new Mat(src, new System.Drawing.Rectangle(frame.rect.X, frame.rect.Y, frame.rect.Width, frame.rect.Height));

                // 2. Extraire le canal alpha si transparent (RGBA)
                var alpha = new Mat();
                if (img.NumberOfChannels == 4)
                {
                    var channels = new VectorOfMat();
                    CvInvoke.Split(img, channels);
                    alpha = channels[3]; // Canal alpha
                }
                else
                {
                    // Si pas de transparence, créer un masque par couleur (ex: fond blanc)
                    using var mask = new Mat();
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
            if (spriteSheet == null)
                return;

            // 1. Charger l’image
            using Mat src = spriteSheet.ToBitmap().ToMat();

            foreach (var frame in frames)
            {
                // 1. Définir la zone d’intérêt (ROI)
                using var img = new Mat(src, new System.Drawing.Rectangle(frame.rect.X, frame.rect.Y, frame.rect.Width, frame.rect.Height));

                // 2. Convertir en HSV (plus stable pour détecter une couleur unie)
                var imgHsv = new Mat();
                CvInvoke.CvtColor(img, imgHsv, ColorConversion.Bgr2Hsv);

                // 3. Définir la couleur du fond (ex: coin supérieur gauche)
                var bgColor = imgHsv.ToImage<Hsv, byte>()[0, 0];//utiliser un échantillon ?

                // 4. Définir une tolérance autour de la couleur du fond
                var lower = new Hsv(bgColor.Hue - 10, Math.Max(bgColor.Satuation - 40, 0), Math.Max(bgColor.Value - 40, 0));
                var upper = new Hsv(bgColor.Hue + 10, Math.Min(bgColor.Satuation + 40, 255), Math.Min(bgColor.Value + 40, 255));

                // 5. Créer un masque du fond
                Image<Hsv, byte> imgHsvImage = imgHsv.ToImage<Hsv, byte>();
                Image<Gray, byte> mask = imgHsvImage.InRange(lower, upper);

                // 6. Inverser le masque (car on veut l’objet)
                mask = mask.Not();

                // 7. Nettoyer le masque (morphologie)
                mask = mask.SmoothGaussian(5);
                CvInvoke.Erode(mask, mask, null, new System.Drawing.Point(-1, -1), 1, BorderType.Default, new MCvScalar());
                CvInvoke.Dilate(mask, mask, null, new System.Drawing.Point(-1, -1), 2, BorderType.Default, new MCvScalar());

                var contours = new VectorOfVectorOfPoint();
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
            if (spriteSheet == null)
                return;

            // 1. Charger l’image
            using Mat src = spriteSheet.ToBitmap().ToMat();

            foreach (var frame in frames)
            {
                // 1. Définir la zone d’intérêt (ROI)
                using var img = new Mat(src, new System.Drawing.Rectangle(frame.rect.X, frame.rect.Y, frame.rect.Width, frame.rect.Height));

                // Convertir en niveaux de gris
                var gray = new Mat();
                CvInvoke.CvtColor(img, gray, ColorConversion.Bgr2Gray);

                // Appliquer un seuillage automatique
                var thresh = new Mat();
                CvInvoke.Threshold(gray, thresh, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);

                // Trouver les contours dans le ROI
                var contours = new VectorOfVectorOfPoint();
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
                    StrokeDashArray = [2],
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
                    StrokeDashArray = [2],
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

                OnPropertyChange(nameof(SelectedFrame));
            };
            animationTimer.Start();
            OnPropertyChange(nameof(FrameDuration));
        }

        private void SetupReloadTimer()
        {
            reloadTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            reloadTimer.Tick += (s, e) =>
            {
                if (spriteChanged)
                {
                    this.Dispatcher.Invoke(new Action(() => { ReLoadImage(); }));
                }
            };
            reloadTimer.Start();
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp"
            };

            if (dlg.ShowDialog() == true)
            {
                LoadImage(dlg.FileName);
            }
        }


        internal void LoadImage(string path)
        {
            if (path == null)
                return;

            var dirname = Path.GetDirectoryName(path);
            var filename = Path.GetFileName(path);

            if(dirname == null || filename == null)
                return;

            spriteChanged = false;
            spritePath = path;
            spriteSheet = new BitmapImage();
            spriteSheet.BeginInit();
            var mem = new MemoryStream();
            using (FileStream file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                file.CopyTo(mem);
            spriteSheet.StreamSource = mem;
            spriteSheet.CacheOption = BitmapCacheOption.OnLoad;
            spriteSheet.EndInit();
            spriteSheetTransparency = spriteSheet.HasActualTransparency();

            MainImage.Source = spriteSheet;
            MainImage.Width = spriteSheet.PixelWidth;
            MainImage.Height = spriteSheet.PixelHeight;
            ImageCanvas.Width = spriteSheet.PixelWidth;
            ImageCanvas.Height = spriteSheet.PixelHeight;

            ClearFrames(); // Optionnel : garde ou efface les frames existants

            spriteSheetSystemWatcher = new FileSystemWatcher(dirname, filename);
            spriteSheetSystemWatcher.BeginInit();
            spriteSheetSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.LastAccess;
            spriteSheetSystemWatcher.Changed += SpriteSheetSystemWatcher_Changed;
            spriteSheetSystemWatcher.EnableRaisingEvents = true;
            spriteSheetSystemWatcher.EndInit();
        }

        internal void ReLoadImage()
        {
            if (spriteSheet == null || spritePath == null)
                return;

            try
            {
                MemoryStream? mem = spriteSheet.StreamSource as MemoryStream;
                if (mem == null)
                    mem = new MemoryStream();

                mem.Seek(0, SeekOrigin.Begin);
                using (FileStream file = File.Open(spritePath, FileMode.Open, FileAccess.Read))
                {
                    file.CopyTo(mem);
                    mem.SetLength(file.Length);
                }

                spriteSheet = new BitmapImage();
                spriteSheet.BeginInit();
                spriteSheet.StreamSource = mem;
                spriteSheet.CacheOption = BitmapCacheOption.OnLoad;
                spriteSheet.EndInit();

                MainImage.Source = spriteSheet;
                MainImage.Width = spriteSheet.PixelWidth;
                MainImage.Height = spriteSheet.PixelHeight;
                ImageCanvas.Width = spriteSheet.PixelWidth;
                ImageCanvas.Height = spriteSheet.PixelHeight;

                spriteChanged = false;
            }
            catch (Exception)
            {
            }
        }

        private void SpriteSheetSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            spriteChanged = true;
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (spriteSheet == null) return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                isDrawing = true;
                startPoint = e.GetPosition(ImageCanvas);

                selectionRect = new System.Windows.Shapes.Rectangle
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    StrokeDashArray = [2],
                    Width = 0,
                    Height = 0
                };
                Canvas.SetLeft(selectionRect, startPoint.X);
                Canvas.SetTop(selectionRect, startPoint.Y);
                ImageCanvas.Children.Add(selectionRect);
                MainImage.CaptureMouse();
            }
            else if(e.MiddleButton == MouseButtonState.Pressed)
            {
                isPanning = true;
                startPoint = e.GetPosition(this);
                MainImage.CaptureMouse();
            }

            e.Handled = true;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing && selectionRect != null)
            {
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
            else if (isPanning && e.MiddleButton == MouseButtonState.Pressed)
            {
                Point currentPos = e.GetPosition(this);
                System.Windows.Vector delta = currentPos - startPoint;

                PanTransform.X += delta.X;
                PanTransform.Y += delta.Y;

                startPoint = currentPos;
            }

            e.Handled = true;
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDrawing)
            {
                isDrawing = false;
                Point endPoint = e.GetPosition(ImageCanvas);

                int x = (int)Math.Min(startPoint.X, endPoint.X);
                int y = (int)Math.Min(startPoint.Y, endPoint.Y);
                int w = (int)Math.Abs(endPoint.X - startPoint.X);
                int h = (int)Math.Abs(endPoint.Y - startPoint.Y);

                // max
                if (spriteSheet != null)
                {
                    if (x + w > spriteSheet.Width)
                        w = (int)spriteSheet.Width - x;
                    if (y + h > spriteSheet.Height)
                        h = (int)spriteSheet.Height - y;
                }

                if (w > 5 && h > 5)
                {
                    var r = new Int32Rect(x, y, w, h);

                    var frame = new Frame { rect = r, frameCount = 1, origin = new Vector2(r.Width / 2.0f, r.Height / 2.0f) };
                    Frames.Add(frame);

                    //if (spriteSheetTransparency == false)//pas d'alpha
                    AdjustBoundFromSolidBackground([frame]);
                    // else
                    //    AdjustBound(new[] { frame });

                    ShowOrigins([frame]);
                }

                ImageCanvas.Children.Remove(selectionRect);
                MainImage.ReleaseMouseCapture();
            }
            else if (isPanning)
            {
                isPanning = false;
                MainImage.ReleaseMouseCapture();
            }

            e.Handled = true;
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

        private void RemoveFrames(IEnumerable<Frame> frames)
        {
            var _frames = frames.ToArray();
            currentFrameIndex = 0;
            foreach (var frame in _frames)
                Frames.Remove(frame);
            HideOrigins(_frames);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                var path = Path.GetFullPath(args[1]);
                LoadImage(path);
            }
        }


        private void ListView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(null);
            StopFrame();
            e.Handled = false;
        }


        private void ListView_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            System.Windows.Vector diff = startPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                var listView = sender as ListView;
                var listViewItem = ((DependencyObject)e.OriginalSource).FindAncestor<ListViewItem>();

                if (listViewItem == null) return;

                var item = (Frame)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                if (item != null)
                {
                    DragDrop.DoDragDrop(listViewItem, item, DragDropEffects.Move);
                }
            }
        }

        private void ListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(Frame)))
            {
                var droppedData = e.Data.GetData(typeof(Frame)) as Frame;
                var listView = sender as ListView;
                var point = e.GetPosition(listView);
                var target = listView?.GetItemAt<Frame>(point);

                if (droppedData == null || target == null || droppedData == target || !Frames.Contains(droppedData))
                    return;

                int removedIdx = Frames.IndexOf(droppedData);
                int targetIdx = Frames.IndexOf(target);

                if (removedIdx < targetIdx)
                {
                    Frames.Insert(targetIdx + 1, droppedData);
                    Frames.RemoveAt(removedIdx);
                }
                else
                {
                    int remIdx = removedIdx + 1;
                    if (Frames.Count + 1 > remIdx)
                    {
                        Frames.Insert(targetIdx, droppedData);
                        Frames.RemoveAt(remIdx);
                    }
                }
            }
        }

        private void ShowOrigins_Click(object sender, RoutedEventArgs e)
        {
            if (((ToggleButton)sender).IsChecked == true)
            {
                ShowOrigins(Frames);
            }
            else
            {
                HideOrigins(Frames);
            }
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
            var wnd = new ExportWindow
            {
                ImageWidth = Frames.Select(p => p.rect.Width).Max(),
                ImageHeight = Frames.Select(p => p.rect.Height).Max(),
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

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

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            RemoveFrames(frameList.SelectedItems.OfType<Frame>());
        }

        public string PlaySymbol
        {
            get
            {
                return animationTimer?.IsEnabled == true ? "||" : "▶︎";
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            NextFrame();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            PauseFrame();
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            PrevFrame();
        }

        private void NextFrame()
        {
            animationTimer?.Stop();

            if (Frames.Count == 0 || spriteSheet == null) return;

            if (currentFrameIndex + 1 >= Frames.Count)
                currentFrameIndex = 0;
            else
                currentFrameIndex++;

            SetFrame(currentFrameIndex);
        }

        private void PauseFrame()
        {
            if (animationTimer?.IsEnabled == true)
                animationTimer?.Stop();
            else
                animationTimer?.Start();

            OnPropertyChange(nameof(PlaySymbol));

            OnPropertyChange(nameof(SelectedFrame));
        }

        private void StopFrame()
        {
            animationTimer?.Stop();

            OnPropertyChange(nameof(PlaySymbol));

            OnPropertyChange(nameof(SelectedFrame));
        }

        private void PrevFrame()
        {
            animationTimer?.Stop();

            if (Frames.Count == 0 || spriteSheet == null) return;

            if (currentFrameIndex - 1 < 0)
                currentFrameIndex = Frames.Count - 1;
            else
                currentFrameIndex--;

            SetFrame(currentFrameIndex);
        }

        private void DockPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else if (e.ButtonState == MouseButtonState.Pressed && e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void UpgradeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/Ace4teaM/animate/blob/main/UPGRADE.md") { UseShellExecute = true });
            }
            catch (Exception)
            {
            }
        }

        internal double zoomFactor = 1.0;
        public double ZoomFactor
        {
            get { return zoomFactor; }
            set
            {
                zoomFactor = value;
                ZoomTransform.ScaleX = zoomFactor;
                ZoomTransform.ScaleY = zoomFactor;
                OnPropertyChange(nameof(ZoomFactor));
            }
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mousePos = e.GetPosition(canvas);

            double zoomFactor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;

            var st = ZoomTransform;
            var tt = PanTransform;

            // Zoom centré sur la souris
            double absX = mousePos.X * st.ScaleX + tt.X;
            double absY = mousePos.Y * st.ScaleY + tt.Y;

            var f = st.ScaleX * zoomFactor;
            if (f > ZoomSlider.Maximum)
                f = ZoomSlider.Maximum;

           // st.ScaleX = f;
          //  st.ScaleY = f;

            ZoomFactor = f;

            tt.X = absX - mousePos.X * ZoomFactor;
            tt.Y = absY - mousePos.Y * ZoomFactor;
        }

        private void Center_Click(object sender, RoutedEventArgs e)
        {
            PanTransform.X = 0;
            PanTransform.Y = 0;
            ZoomFactor = 1;
        }
    }
}