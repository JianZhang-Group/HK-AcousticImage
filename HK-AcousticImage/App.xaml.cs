using System.ComponentModel;
using System.Windows;
using Prism.Ioc;
using Prism.Unity;

namespace HK_AcousticImage
{
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册依赖
        }
    }

}
