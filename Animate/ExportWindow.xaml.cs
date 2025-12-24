using System.ComponentModel;
using System.Numerics;
using System.Windows;

namespace Animate
{
    /// <summary>
    /// Logique d'interaction pour Export.xaml
    /// </summary>
    public partial class ExportWindow : Window, INotifyPropertyChanged
    {
        // Liste des valeurs possibles pour les différentes résolutions
        private int[] sliderValues = { 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

        private int imageHeight;
        private int imageWidth;

        public string AlignmentHoriz { get; set; } = "Center";
        public string AlignmentVert { get; set; } = "Center";
        public string ImagePrefix { get; set; } = "sprite_";
        public int ImageHeight { get => imageHeight; set { imageHeight = value; ResolutionChange(); OnPropertyChange(nameof(AdjustedImageHeight)); } }
        public int ImageWidth { get => imageWidth; set { imageWidth = value; ResolutionChange(); OnPropertyChange(nameof(AdjustedImageWidth)); } }
        public bool? AdjustSize { get; set; } = true;
        public int AdjustedImageHeight { get { return (int)BitOperations.RoundUpToPowerOf2((uint)ImageHeight); } }
        public int AdjustedImageWidth { get { return (int)BitOperations.RoundUpToPowerOf2((uint)ImageWidth); } }
        public int AdjustedAndResizedImageHeight { get { return divSlider != null ? sliderValues[(int)divSlider.Value] : 0; } }
        public int AdjustedAndResizedImageWidth { get { return divSlider != null ? (int)((((double)sliderValues[(int)divSlider.Value] / AdjustedImageHeight)) * AdjustedImageWidth) : 0; } }

        public ExportWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        internal void ResolutionChange()
        {
            int i = 0;
            while (i < sliderValues.Length && sliderValues[i] < AdjustedImageHeight)
                i++;

            divSlider.Minimum = 0;
            divSlider.Maximum = i;
            divSlider.Value = i;

            var closestValue = sliderValues[i];

            // Initialisation du Slider avec la première valeur
            OnPropertyChange(nameof(AdjustedAndResizedImageWidth));
            OnPropertyChange(nameof(AdjustedAndResizedImageHeight));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;//CloseWindow
        }
 
        public ExportSettings GetExportSettings()
        {
            return new ExportSettings
            {
                AlignmentHoriz = AlignmentHoriz,
                AlignmentVert = AlignmentVert,
                ImagePrefix = ImagePrefix,
                ImageWidth = ImageWidth,
                ImageHeight = ImageHeight,
                AdjustSize = AdjustSize ?? true,
                SliderValue = divSlider != null ? (int)divSlider.Value : 0
            };
        }

        public void SetExportSettings(ExportSettings settings)
        {
            if (settings == null) return;

            AlignmentHoriz = settings.AlignmentHoriz;
            AlignmentVert = settings.AlignmentVert;
            ImagePrefix = settings.ImagePrefix;
            ImageWidth = settings.ImageWidth;
            ImageHeight = settings.ImageHeight;
            AdjustSize = settings.AdjustSize;
            
            if (divSlider != null)
            {
                ResolutionChange();
                if (settings.SliderValue <= divSlider.Maximum)
                    divSlider.Value = settings.SliderValue;
            }
        }

        // Gestion de l'événement ValueChanged du Slider
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Trouver la valeur la plus proche dans la liste des valeurs définies
            var closestValue = sliderValues[(int)divSlider.Value];

            // Mettre à jour l'affichage du TextBlock
            OnPropertyChange(nameof(AdjustedAndResizedImageWidth));
            OnPropertyChange(nameof(AdjustedAndResizedImageHeight));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ResolutionChange();
        }
    }
}
