using HK_AcousticImage.ViewModels;
using System.Windows;

namespace HK_AcousticImage
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var vm = new MainViewModel();
            this.DataContext = vm;

            // 将 MediaPlayer 绑定到 VideoView
            videoView.MediaPlayer = vm.GetMediaPlayer();
        }
    }
}