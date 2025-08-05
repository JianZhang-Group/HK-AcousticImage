using HK_AcousticImage_Api;
using LibVLCSharp.Shared;
using NLog;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using NLogLevel = NLog.LogLevel;
using VlcLogLevel = LibVLCSharp.Shared.LogLevel;

namespace HK_AcousticImage.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private HKAcousticImageDevice? device;
        private AlarmHttpServer? alarmServer;

        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        public DelegateCommand PlayRtspCommand { get; }
        public DelegateCommand StopRtspCommand { get; }


        private string _deviceIp = "192.168.31.64";
        public string DeviceIp
        {
            get => _deviceIp;
            set => SetProperty(ref _deviceIp, value);
        }

        private string _username = "admin";
        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        private string _password = "";
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        private string _hostPort = "8080";
        public string HostPort
        {
            get => _hostPort;
            set => SetProperty(ref _hostPort, value);
        }

        private string _hostId = "1";
        public string HostId
        {
            get => _hostId;
            set => SetProperty(ref _hostId, value);
        }

        public ObservableCollection<string> ProtocolOptions { get; }
= new ObservableCollection<string> { "HTTP", "HTTPS" };
        private string _protocol = "HTTP"; // 默认值
        public string Protocol
        {
            get => _protocol;
            set => SetProperty(ref _protocol, value);
        }

        private string _hostUrl = "192.168.31.10";
        public string HostUrl
        {
            get => _hostUrl;
            set => SetProperty(ref _hostUrl, value);
        }

        private string _threshold = "";
        public string Threshold
        {
            get => _threshold;
            set => SetProperty(ref _threshold, value);
        }

        private string _duration = "";
        public string Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public ObservableCollection<string> LogMessages { get; } = new ObservableCollection<string>();

        public ICommand LoginCommand { get; }
        public ICommand GetAlarmHostsCommand { get; }
        public ICommand ConfigAlarmHostCommand { get; }
        public ICommand TestAlarmHostCommand { get; }
        public ICommand StartSoundCommand { get; }
        public ICommand StopSoundCommand { get; }
        public ICommand StartAlarmServerCommand { get; }
        public ICommand StopAlarmServerCommand { get; }

        public MainViewModel()
        {
            Core.Initialize();
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);

            LoginCommand = new DelegateCommand(async () => await LoginAsync());
            GetAlarmHostsCommand = new DelegateCommand(async () => await GetAlarmHostsAsync());
            ConfigAlarmHostCommand = new DelegateCommand(async () => await ConfigAlarmHostAsync());
            TestAlarmHostCommand = new DelegateCommand(async () => await TestAlarmHostAsync());
            StartSoundCommand = new DelegateCommand(async () => await StartSoundAsync());
            StopSoundCommand = new DelegateCommand(async () => await StopSoundAsync());
            StartAlarmServerCommand = new DelegateCommand(StartAlarmServer);
            StopAlarmServerCommand = new DelegateCommand(StopAlarmServer);
            PlayRtspCommand = new DelegateCommand(OnPlayRtsp);
            StopRtspCommand = new DelegateCommand(OnStopRtsp);
        }

        private async Task LoginAsync()
        {
            string msg = $"开始登录，设备IP：{DeviceIp}, 用户名：{Username}";
            LogAndRecordInfo(msg);

            device = new HKAcousticImageDevice(DeviceIp, Username, Password);

            var (success, message) = await device.CheckLoginAsync();

            LogAndRecord(success ? NLogLevel.Info : NLogLevel.Warn, message);

            if (!success)
            {
                device = null;
            }
        }

        private async Task GetAlarmHostsAsync()
        {
            if (!CheckDevice())
                return;

            LogAndRecordInfo("开始获取监听主机配置...");

            var hosts = await device!.GetAlarmHostsAsync();

            if (hosts != null)
            {
                LogAndRecordInfo("监听主机配置:\n" + hosts);
            }
            else
            {
                LogAndRecordWarn("❌ 获取监听主机配置失败");
            }
        }

        private async Task ConfigAlarmHostAsync()
        {
            if (!CheckDevice())
                return;

            if (!int.TryParse(HostPort, out int port))
            {
                LogAndRecordWarn("端口号输入无效");
                return;
            }

            LogAndRecordInfo($"开始配置监听主机: IP={HostUrl}, 端口={port}");

            bool success = await device!.ConfigAlarmHostAsync(HostUrl, port);

            if (success)
                LogAndRecordInfo("监听主机配置成功");
            else
                LogAndRecordWarn("监听主机配置失败");
        }

        private async Task TestAlarmHostAsync()
        {
            if (!CheckDevice())
                return;

            LogAndRecordInfo("开始测试监听主机连接...");

            bool success = await device!.TestHttpHostAsync();

            if (success)
                LogAndRecordInfo("监听主机测试成功");
            else
                LogAndRecordWarn("监听主机测试失败");
        }

        private async Task StartSoundAsync()
        {
            if (!CheckDevice())
                return;

            LogAndRecordInfo("启动声源检测...");

            await device!.StartSoundLocationAsync();
        }

        private async Task StopSoundAsync()
        {
            if (!CheckDevice())
                return;

            LogAndRecordInfo("停止声源检测...");

            await device!.StopSoundLocationAsync();
        }

        private bool CheckDevice()
        {
            if (device == null)
            {
                LogAndRecordWarn("❌ 请先登录设备");
                return false;
            }
            return true;
        }

        // 启动报警服务
        private void StartAlarmServer()
        {
            if (alarmServer != null)
            {
                LogAndRecordWarn("报警服务已经在运行中");
                return;
            }

            // 默认监听本机所有接口的8080端口，你可扩展为绑定界面输入端口
            string url = $"http://+:{HostPort}/";
            alarmServer = new AlarmHttpServer(url);

            alarmServer.AlarmReceived += AlarmServer_AlarmReceived;

            alarmServer.Start();

            LogAndRecordInfo($"报警服务启动，监听地址：{url}");
        }

        // 停止报警服务
        private void StopAlarmServer()
        {
            if (alarmServer == null)
            {
                LogAndRecordWarn("报警服务未运行");
                return;
            }

            alarmServer.AlarmReceived -= AlarmServer_AlarmReceived;

            alarmServer.Stop();
            alarmServer = null;

            LogAndRecordInfo("报警服务已停止");
        }

        // 收到报警事件回调
        private void AlarmServer_AlarmReceived(object? sender, AlarmEventArgs e)
        {
            // UI 线程安全考虑，使用 Dispatcher，假设你的 ViewModel 有 Dispatcher 或用 SynchronizationContext
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                string msg = $"🔔 报警事件：类型={e.EventType}, 描述={e.EventDescription}, 时间={e.DateTime}, 设备={e.DeviceIp}";
                AddLog(msg);
                logger.Info(msg);
            });
        }

        private void OnPlayRtsp()
        {
            try
            {
                string rtspUrl = $"rtsp://{Username}:{Password}@{DeviceIp}:554/ISAPI/Streaming/Channels/101";
                var media = new Media(_libVLC, rtspUrl, FromType.FromLocation);
                _mediaPlayer.Play(media);
                MessageBox.Show("开始播放视频");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"播放失败: {ex.Message}");
            }
        }

        private void OnStopRtsp()
        {
            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Stop();
                MessageBox.Show("已停止播放");
            }
        }

        public MediaPlayer GetMediaPlayer() => _mediaPlayer;


        private void LogAndRecordInfo(string message)
        {
            AddLog(message);
            logger.Info(message);
        }

        private void LogAndRecordWarn(string message)
        {
            AddLog(message);
            logger.Warn(message);
        }

        private void LogAndRecord(NLogLevel level, string message)
        {
            AddLog(message);
            logger.Log(level, message);
        }

        private void AddLog(string message)
        {
            LogMessages.Add($"{System.DateTime.Now:HH:mm:ss} - {message}");
        }
    }
}
