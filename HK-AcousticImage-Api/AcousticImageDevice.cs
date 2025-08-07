using Newtonsoft.Json;  // 需要安装：dotnet add package Newtonsoft.Json
using NLog;
using System.Net;
using System.Net.Http;
using System.Text;

namespace HK_AcousticImage_Api
{
    public class HKAcousticImageDevice
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly string deviceIp;
        private readonly string username;
        private readonly string password;
        private readonly int audioInId;
        private readonly string security;
        private readonly string iv;
        private readonly HttpClient httpClient;

        public HKAcousticImageDevice(string deviceIp, string username, string password,
            int audioInId = 1, string security = "none", string iv = "0")
        {
            this.deviceIp = deviceIp;
            this.username = username;
            this.password = password;
            this.audioInId = audioInId;
            this.security = security;
            this.iv = iv;

            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(username, password),
                PreAuthenticate = true
            };

            httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }
        #region 时间配置
        // 时间管理能力
        public async Task<string?> GetTimeCapabilitiesAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/System/time/capabilities";
            var resp = await httpClient.GetAsync(url);
            var respText = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("✅ 获取时间管理能力成功");
                return respText;
            }

            logger.Warn($"❌ 获取时间管理能力失败: {(int)resp.StatusCode}\n{respText}");
            return null;
        }
        // 获取设备当前时间设置
        public async Task<string?> GetDeviceTimeAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/System/time";
            var resp = await httpClient.GetAsync(url);
            var respText = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("✅ 获取设备时间成功");
                return respText;
            }

            logger.Warn($"❌ 获取设备时间失败: {(int)resp.StatusCode}\n{respText}");
            return null;
        }
        // 配置设备时间参数
        public async Task<bool> SetDeviceTimeAsync(string timeMode = "manual", string? localTime = null, string? timeZone = null)
        {
            string url = $"http://{deviceIp}/ISAPI/System/time";

            string payload = $@"
        <?xml version=""1.0"" encoding=""UTF-8""?>
        <Time xmlns=""http://www.hikvision.com/ver20/XMLSchema"" version=""2.0"">
            <timeMode>{timeMode}</timeMode>
            {(localTime != null ? $"<localTime>{localTime}</localTime>" : "")}
            {(timeZone != null ? $"<timeZone>{timeZone}</timeZone>" : "")}
        </Time>";

            var content = new StringContent(payload, Encoding.UTF8, "application/xml");
            var resp = await httpClient.PutAsync(url, content);
            var respText = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("✅ 设置设备时间成功");
                return true;
            }

            logger.Warn($"❌ 设置设备时间失败: {(int)resp.StatusCode}\n{respText}");
            return false;
        }
        // 获取全部 NTP 服务器
        public async Task<string?> GetAllNtpServersAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/System/time/ntpServers";
            var resp = await httpClient.GetAsync(url);
            var respText = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("✅ 获取 NTP 服务器列表成功");
                return respText;
            }

            logger.Warn($"❌ 获取 NTP 服务器失败: {(int)resp.StatusCode}\n{respText}");
            return null;
        }
        // 配置单个 NTP 服务器
        public async Task<bool> ConfigNtpServerAsync(
            int id = 1,
            string addressingType = "hostname",
            string hostName = "ntp.example.com",
            string ipAddress = "",
            string ipv6Address = "",
            int port = 123,
            int interval = 1440)
        {
            string url = $"http://{deviceIp}/ISAPI/System/time/ntpServers/{id}";

            string payload = $@"
        <?xml version=""1.0"" encoding=""UTF-8""?>
        <NTPServer xmlns=""http://www.isapi.org/ver20/XMLSchema"" version=""2.0"">
            <id>{id}</id>
            <addressingFormatType>{addressingType}</addressingFormatType>
            {(addressingType == "hostname" ? $"<hostName>{hostName}</hostName>" : $"<ipAddress>{ipAddress}</ipAddress>")}
            {(string.IsNullOrWhiteSpace(ipv6Address) ? "" : $"<ipv6Address>{ipv6Address}</ipv6Address>")}
            <portNo>{port}</portNo>
            <synchronizeInterval>{interval}</synchronizeInterval>
        </NTPServer>";

            var content = new StringContent(payload, Encoding.UTF8, "application/xml");
            var resp = await httpClient.PutAsync(url, content);
            var respText = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info($"✅ 设置 NTP 服务器成功：{hostName}/{ipAddress}:{port}");
                return true;
            }

            logger.Warn($"❌ 设置 NTP 服务器失败: {(int)resp.StatusCode}\n{respText}");
            return false;
        }
        #endregion

        #region 登录配置
        // 检查设备账号密码是否正确
        public async Task<(bool, string)> CheckLoginAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/System/status";
            try
            {
                var resp = await httpClient.GetAsync(url);
                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    logger.Info("✅ 登录成功");
                    return (true, "登录成功");
                }
                else if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    logger.Warn("❌ 用户名或密码错误");
                    return (false, "用户名或密码错误");
                }
                else
                {
                    logger.Warn($"❌ 登录失败，状态码: {(int)resp.StatusCode}");
                    return (false, $"登录失败，状态码: {(int)resp.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "连接异常");
                return (false, $"连接异常: {ex.Message}");
            }
        }
        #endregion

        #region 监听配置
        // 检查监听能力
        public async Task<bool> CheckCapabilitiesAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/Event/notification/httpHosts/capabilities";
            var resp = await httpClient.GetAsync(url);
            var content = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == HttpStatusCode.OK && content.Contains("HttpHostNotificationCap"))
            {
                logger.Info("✅ 设备支持监听主机参数配置");
                return true;
            }
            logger.Warn($"❌ 设备不支持监听方式: {(int)resp.StatusCode}");
            return false;
        }

        // 获取所有监听配置
        public async Task<string?> GetAlarmHostsAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/Event/notification/httpHosts?security={security}&iv={iv}";
            var resp = await httpClient.GetAsync(url);
            var content = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("📋 已有监听配置:");
                logger.Debug(content.Length > 500 ? content.Substring(0, 500) : content);
                return content;
            }
            logger.Warn($"❌ 获取监听配置失败: {(int)resp.StatusCode}\n{content}");
            return null;
        }

        // 配置监听主机
        public async Task<bool> ConfigAlarmHostAsync(string hostIp, int port = 8080, int hostId = 1)
        {
            string url = $"http://{deviceIp}/ISAPI/Event/notification/httpHosts/{hostId}?security={security}&iv={iv}";
            string payload = $@"
                <?xml version=""1.0"" encoding=""UTF-8""?>
                <HttpHostNotification version=""2.0"" xmlns=""http://www.hikvision.com/ver20/XMLSchema"">
                    <id>{hostId}</id>
                    <enabled>true</enabled>
                    <addressingFormatType>ipaddress</addressingFormatType>
                    <ipAddress>{hostIp}</ipAddress>
                    <portNo>{port}</portNo>
                    <url>/alarm</url>
                    <protocolType>HTTP</protocolType>
                    <method>POST</method>
                    <parameterFormatType>XML</parameterFormatType>
                    <httpAuthenticationMethod>none</httpAuthenticationMethod>
                </HttpHostNotification>";

            var content = new StringContent(payload, Encoding.UTF8, "application/xml");
            var resp = await httpClient.PutAsync(url, content);
            var respText = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info($"✅ 配置成功，设备将推送报警到 http://{hostIp}:{port}/alarm");
                return true;
            }
            logger.Warn($"❌ 配置失败: {(int)resp.StatusCode}\n{respText}");
            return false;
        }

        // 测试监听主机连通性
        public async Task<bool> TestHttpHostAsync(int hostId = 1)
        {
            string url = $"http://{deviceIp}/ISAPI/Event/notification/httpHosts/{hostId}/test";
            try
            {
                var resp = await httpClient.PostAsync(url, null);
                var content = await resp.Content.ReadAsStringAsync();
                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    logger.Info("✅ 监听主机测试成功");
                    logger.Debug("返回内容：" + content);
                    return true;
                }
                else
                {
                    logger.Warn($"❌ 监听主机测试失败，状态码: {(int)resp.StatusCode}");
                    logger.Debug("返回内容：" + content);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "监听主机测试异常");
                return false;
            }
        }
        #endregion

        #region 声源检测
        // 启动声源检测
        public async Task<bool> StartSoundLocationAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/System/SoundSourceLocation/AudioIn/{audioInId}/SoundSourceLocationRuleParams?format=json";
            var payload = JsonConvert.SerializeObject(new { enabled = true });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await httpClient.PutAsync(url, content);
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("✅ 声源检测已启动");
                return true;
            }
            else
            {
                logger.Warn("❌ 启动失败: " + await resp.Content.ReadAsStringAsync());
                return false;
            }          
        }

        // 停止声源检测
        public async Task<bool> StopSoundLocationAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/System/SoundSourceLocation/AudioIn/{audioInId}/SoundSourceLocationRuleParams?format=json";
            var payload = JsonConvert.SerializeObject(new { enabled = false });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await httpClient.PutAsync(url, content);
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("✅ 声源检测已停止");
                return true;
            }

            else
            {
                logger.Warn("❌ 停止失败: " + await resp.Content.ReadAsStringAsync());
                return false;
            }   
        }
        #endregion

        #region 音频侦测
        // 获取音频侦测能力
        public async Task<string?> GetAudioDetectionCapabilitiesAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/Smart/AudioDetection/channels/{audioInId}/capabilities";
            var resp = await httpClient.GetAsync(url);
            var content = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("📊 音频侦测能力参数(XML)：" + content);
                return content;
            }
            logger.Warn("❌ 获取音频侦测能力失败: " + (int)resp.StatusCode + "\n" + content);
            return null;
        }

        // 获取音频侦测参数
        public async Task<string?> GetAudioDetectionParamsAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/Smart/AudioDetection/channels/{audioInId}";
            var resp = await httpClient.GetAsync(url);
            var content = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("📋 当前音频侦测参数(XML)：" + content);
                return content;
            }
            logger.Warn("❌ 获取音频侦测参数失败: " + (int)resp.StatusCode + "\n" + content);
            return null;
        }

        // 设置音频侦测参数
        public async Task<string?> SetAudioDetectionParamsAsync(
            int audioMode = 3,
            int decibelThreshold = 10,
            float decibelThresholdDuration = 10,
            bool frequencyEnabled = false,
            int frequencyThreshold = 0,
            float frequencyThresholdDuration = 10)
        {
            string url = $"http://{deviceIp}/ISAPI/Smart/AudioDetection/channels/{audioInId}";

            // 构造 XML 报文
            string payload = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                <AudioDetection xmlns=""http://www.hikvision.com/ver20/XMLSchema"" version=""2.0"">
                <id>{audioInId}</id>
                <audioMode>{audioMode}</audioMode>
                <decibelThreshold>{decibelThreshold}</decibelThreshold>
                <decibelThresholdDuration>{decibelThresholdDuration}</decibelThresholdDuration>
                <frequencyThresholdDetectionParams>
                <enabled>{frequencyEnabled.ToString().ToLower()}</enabled>
                <frequencyThreshold>{frequencyThreshold}</frequencyThreshold>
                <frequencyThresholdDuration>{frequencyThresholdDuration}</frequencyThresholdDuration>
                </frequencyThresholdDetectionParams>
                </AudioDetection>";

            var content = new StringContent(payload, Encoding.UTF8, "application/xml");
            var resp = await httpClient.PutAsync(url, content);
            var respText = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("✅ 音频侦测参数设置成功(XML)：" + respText);
                return respText;
            }
            logger.Warn("❌ 设置音频侦测参数失败: " + (int)resp.StatusCode + "\n" + respText);
            return null;
        }
        #endregion

        #region 声学检漏
        // 获取声学检漏能力
        public async Task<dynamic?> GetAcousticCapabilitiesAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/System/AcousticLeakDetection/AudioIn/{audioInId}/AlarmAnalysisParam/capabilities?format=json";
            var resp = await httpClient.GetAsync(url);
            var content = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("📊 声学检漏能力：" + content);
                return JsonConvert.DeserializeObject(content);
            }
            logger.Warn("❌ 获取声学检漏能力失败: " + (int)resp.StatusCode + "\n" + content);
            return null;
        }

        // 获取声学检漏参数
        public async Task<dynamic?> GetAcousticParamsAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/System/AcousticLeakDetection/AudioIn/{audioInId}/AlarmAnalysisParam?format=json";
            var resp = await httpClient.GetAsync(url);
            var content = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("📋 当前声学检漏参数：" + content);
                return JsonConvert.DeserializeObject(content);
            }
            logger.Warn("❌ 获取声学检漏参数失败: " + (int)resp.StatusCode + "\n" + content);
            return null;
        }

        // 设置声学检漏参数
        public async Task<dynamic?> SetAcousticParamsAsync(int filterTime = 60, int analysisTime = 15)
        {
            string url = $"http://{deviceIp}/ISAPI/System/AcousticLeakDetection/AudioIn/{audioInId}/AlarmAnalysisParam?format=json";
            var payload = new { filterTime, analysisTime };
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = await httpClient.PutAsync(url, content);
            var respText = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("✅ 设置成功：" + respText);
                return JsonConvert.DeserializeObject(respText);
            }
            logger.Warn("❌ 设置失败: " + (int)resp.StatusCode + "\n" + respText);
            return null;
        }

        // 获取气体泄漏检测规则
        public async Task<dynamic?> GetAcousticLeakDetectionAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/System/AcousticLeakDetection/AudioIn/{audioInId}/GasLeakageRuleParams/capabilities?format=json";
            var resp = await httpClient.GetAsync(url);
            var content = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                logger.Info("📊 气体泄漏检测规则：" + content);
                return JsonConvert.DeserializeObject(content);
            }
            logger.Warn("❌ 获取气体泄漏检测规则失败: " + (int)resp.StatusCode + "\n" + content);
            return null;
        }
        #endregion
    }
}
