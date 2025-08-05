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

        // 启动声源检测
        public async Task StartSoundLocationAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/System/SoundSourceLocation/AudioIn/{audioInId}/SoundSourceLocationRuleParams?format=json";
            var payload = JsonConvert.SerializeObject(new { enabled = true });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await httpClient.PutAsync(url, content);
            if (resp.StatusCode == HttpStatusCode.OK)
                logger.Info("✅ 声源检测已启动");
            else
                logger.Warn("❌ 启动失败: " + await resp.Content.ReadAsStringAsync());
        }

        // 停止声源检测
        public async Task StopSoundLocationAsync()
        {
            string url = $"http://{deviceIp}/ISAPI/System/SoundSourceLocation/AudioIn/{audioInId}/SoundSourceLocationRuleParams?format=json";
            var payload = JsonConvert.SerializeObject(new { enabled = false });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await httpClient.PutAsync(url, content);
            if (resp.StatusCode == HttpStatusCode.OK)
                logger.Info("✅ 声源检测已停止");
            else
                logger.Warn("❌ 停止失败: " + await resp.Content.ReadAsStringAsync());
        }

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
    }
}
