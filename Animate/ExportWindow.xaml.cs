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
        private int imageHeight;
        private int imageWidth;

        public int ImageHeight { get => imageHeight; set { imageHeight = value; PropertyChanged?.Invoke(value, new PropertyChangedEventArgs(nameof(AdjustedImageHeight))); } }
        public int ImageWidth { get => imageWidth; set { imageWidth = value; PropertyChanged?.Invoke(value, new PropertyChangedEventArgs(nameof(AdjustedImageWidth))); } }
        public bool? AdjustSize { get; set; }
        public int AdjustedImageHeight { get { return (int)BitOperations.RoundUpToPowerOf2((uint)ImageHeight); } }
        public int AdjustedImageWidth { get { return (int)BitOperations.RoundUpToPowerOf2((uint)ImageWidth); } }

        public ExportWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;//CloseWindow
        }
    }
}
