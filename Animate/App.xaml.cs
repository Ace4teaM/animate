using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Animate
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.MainWindow = new MainWindow();
            this.MainWindow.Show();

            //if(e.Args.Length > 1)
            //    ((MainWindow)this.MainWindow).LoadImage(e.Args[1]);
        }
    }
}
