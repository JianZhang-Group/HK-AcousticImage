using HK_AcousticImage.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace HK_AcousticImage
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel vm; // 声明为字段

        public MainWindow()
        {
            InitializeComponent();

            vm = new MainViewModel(); // 初始化字段
            this.DataContext = vm;

            // 将 MediaPlayer 绑定到 VideoView
            videoView.MediaPlayer = vm.GetMediaPlayer();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            vm.Cleanup();
        }
    }
}
