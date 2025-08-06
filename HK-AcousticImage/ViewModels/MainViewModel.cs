using HK_AcousticImage_Api;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using Newtonsoft.Json;
using NLog;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using NLogLevel = NLog.LogLevel;
using VlcLogLevel = LibVLCSharp.Shared.LogLevel;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace HK_AcousticImage.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private HKAcousticImageDevice? device;
        private AlarmHttpServer? alarmServer;

        private LibVLC _libVLC;
        private VlcMediaPlayer _mediaPlayer;
        public DelegateCommand PlayRtspCommand { get; }
        public DelegateCommand StopRtspCommand { get; }

        private int[] filterTimeOptions = new int[] { 60, 120 };
        private int filterTimeIndex = 0;


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

        private string _password = "HK@minshi";
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

        public ObservableCollection<string> ProtocolOptions { get; }= new ObservableCollection<string> { "HTTP", "HTTPS" };
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

        private string _acousticParamsResult = "未获取";
        public string AcousticParamsResult
        {
            get => _acousticParamsResult;
            set => SetProperty(ref _acousticParamsResult, value);
        }

        private int _filterTime = 60;
        public int FilterTime
        {
            get => _filterTime;
            set => SetProperty(ref _filterTime, value);
        }

        private int _analysisTime = 10;
        public int AnalysisTime
        {
            get => _analysisTime;
            set => SetProperty(ref _analysisTime, value);
        }

        //public ObservableCollection<string> LogMessages { get; } = new ObservableCollection<string>();
        public ObservableCollection<LogEntry> LogMessages { get; } = new();

        public ICommand LoginCommand { get; }
        public ICommand GetAlarmHostsCommand { get; }
        public ICommand ConfigAlarmHostCommand { get; }
        public ICommand TestAlarmHostCommand { get; }
        public ICommand StartSoundCommand { get; }
        public ICommand StopSoundCommand { get; }
        public ICommand StartAlarmServerCommand { get; }
        public ICommand StopAlarmServerCommand { get; }
        public ICommand GetAcousticParamsCommand { get; }
        public ICommand SetAcousticParamsCommand { get; }

        public MainViewModel()
        {
            Core.Initialize();
            _libVLC = new LibVLC();
            _mediaPlayer = new VlcMediaPlayer(_libVLC);

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
            GetAcousticParamsCommand = new DelegateCommand(async () => await GetAcousticParamsAsync());
            SetAcousticParamsCommand = new DelegateCommand(async () => await SetAcousticParamsAsync());
        }

        private async Task GetAcousticParamsAsync()
        {
            if (!CheckDevice())
                return;

            LogAndRecordInfo("开始获取声学检漏参数...");

            var result = await device!.GetAcousticParamsAsync();

            if (result != null)
            {

                // 自动赋值
                FilterTime = (int)(result.filterTime ?? FilterTime);
                AnalysisTime = (int)(result.analysisTime ?? AnalysisTime);

                AcousticParamsResult = JsonConvert.SerializeObject(result, Formatting.Indented);
                LogAndRecordInfo("获取成功：" + AcousticParamsResult);
            }
            else
            {
                AcousticParamsResult = "获取失败";
                LogAndRecordWarn("❌ 获取声学检漏参数失败");
            }
        }

        private async Task SetAcousticParamsAsync()
        {
            if (!CheckDevice())
                return;

            LogAndRecordInfo($"开始设置声学检漏参数，过滤时长={FilterTime}，分析时长={AnalysisTime}");

            var result = await device!.SetAcousticParamsAsync(FilterTime, AnalysisTime);

            if (result != null)
            {

                AcousticParamsResult = JsonConvert.SerializeObject(result, Formatting.Indented);
                LogAndRecordInfo("设置成功：" + AcousticParamsResult);
            }
            else
            {
                AcousticParamsResult = "设置失败";
                LogAndRecordWarn("❌ 设置声学检漏参数失败");
            }
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

            // 默认监听本机所有接口的8080端口
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
            Application.Current.Dispatcher.Invoke(async () =>
            {
                string msg = $"🔔 报警事件：事件类型={e.EventType}, 描述={e.EventDescription}, 报警类型={e.AlarmType}, 时间={e.DateTime}, 设备={e.DeviceIp}";
                LogAndRecordWarn(msg);

                try
                {
                    // 1. 停止采集
                    LogAndRecordInfo("收到报警，先停止声源检测...");
                    await StopSoundAsync();

                    // 切换 FilterTime
                    FilterTime = filterTimeOptions[filterTimeIndex];
                    filterTimeIndex = (filterTimeIndex + 1) % filterTimeOptions.Length;  // 轮换下一个

                    // 2. 重新设定参数
                    LogAndRecordInfo("重新设定声学检漏参数...");
                    await SetAcousticParamsAsync();

                    // 3. 再次启动采集
                    LogAndRecordInfo("重新启动声源检测...");
                    await StartSoundAsync();

                    LogAndRecordInfo("报警处理流程完成 ✅");
                }
                catch (Exception ex)
                {
                    LogAndRecordWarn($"报警处理失败：{ex.Message}");
                }
            });
        }


        private void OnPlayRtsp()
        {
            try
            {
                string rtspUrl = $"rtsp://{Username}:{Password}@{DeviceIp}:554/ISAPI/Streaming/Channels/101";
                var media = new Media(_libVLC, rtspUrl, FromType.FromLocation);
                _mediaPlayer.Play(media);
                LogAndRecordInfo("视频播放已开始");
            }
            catch (Exception ex)
            {
                LogAndRecordWarn($"播放失败: {ex.Message}");
            }
        }

        private void OnStopRtsp()
        {
            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Stop();
                LogAndRecordWarn("已停止播放");
            }
        }



        public VlcMediaPlayer GetMediaPlayer() => _mediaPlayer;


        private void LogAndRecordInfo(string message)
        {
            AddLog("INFO", message);
            logger.Info(message);
        }

        private void LogAndRecordWarn(string message)
        {
            AddLog("WARN", message);
            logger.Warn(message);
        }

        private void LogAndRecord(NLogLevel level, string message)
        {
            AddLog("INFO",message);
            logger.Log(level, message);
        }

        private void AddLog(string level, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogMessages.Add(new LogEntry { Level = level, Message = message });
                if (LogMessages.Count > 10) // 避免爆内存
                    LogMessages.RemoveAt(0);
            });
        }


    }

    public class LogEntry
    {
        public string Time { get; set; } = DateTime.Now.ToString("HH:mm:ss");
        public string Level { get; set; } = "INFO";
        public string Message { get; set; } = "";
    }

    public class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string level = value?.ToString()?.ToUpper() ?? "INFO";
            return level switch
            {
                "ERROR" => Brushes.Red,
                "WARN" => Brushes.Orange,
                "INFO" => Brushes.Green,
                _ => Brushes.Black,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public static class ListBoxAutoScrollBehavior
    {
        public static readonly DependencyProperty AutoScrollProperty =
            DependencyProperty.RegisterAttached(
                "AutoScroll",
                typeof(bool),
                typeof(ListBoxAutoScrollBehavior),
                new PropertyMetadata(false, OnAutoScrollChanged));

        public static bool GetAutoScroll(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollProperty);
        }

        public static void SetAutoScroll(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollProperty, value);
        }

        private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListBox listBox)
            {
                if ((bool)e.NewValue)
                {
                    listBox.Loaded += (s, ev) =>
                    {
                        if (listBox.Items.Count > 0)
                            listBox.ScrollIntoView(listBox.Items[^1]);
                    };

                    listBox.TargetUpdated += (s, ev) =>
                    {
                        if (listBox.Items.Count > 0)
                            listBox.ScrollIntoView(listBox.Items[^1]);
                    };

                    listBox.ItemContainerGenerator.ItemsChanged += (s, ev) =>
                    {
                        if (listBox.Items.Count > 0)
                            listBox.ScrollIntoView(listBox.Items[^1]);
                    };
                }
            }
        }
    }
}
