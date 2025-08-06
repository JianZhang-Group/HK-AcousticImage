using HttpMultipartParser;
using NLog;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace HK_AcousticImage_Api
{
    // 定义报警事件的参数
    public class AlarmEventArgs : EventArgs
    {
        public string EventType { get; set; }
        public string EventDescription { get; set; }
        public string EventState { get; set; }
        public string DateTime { get; set; }
        public string ChannelId { get; set; }
        public string ChannelName { get; set; }
        public string DeviceIp { get; set; }
        public string MacAddress { get; set; }
        public string RawXml { get; set; }

        // 新增字段
        public string ActivePostCount { get; set; }
        public string AlarmType { get; set; }
        public string ResourcesContentType { get; set; }
        public string ResourcesContent { get; set; }
        public string ResourcesFormatType { get; set; }
        public string PictureHeight { get; set; }
        public string PictureWidth { get; set; }
    }

    public class AlarmHttpServer
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly HttpListener listener;
        private Thread serverThread;
        private bool isRunning = false;

        // 新增报警事件
        public event EventHandler<AlarmEventArgs> AlarmReceived;

        public AlarmHttpServer(string host = "http://+:8080/")
        {
            listener = new HttpListener();
            listener.Prefixes.Add(host);
        }

        public void Start()
        {
            isRunning = true;
            serverThread = new Thread(RunServer);
            serverThread.IsBackground = true;
            serverThread.Start();
        }

        private void RunServer()
        {
            try
            {
                listener.Start();
                logger.Info($"🚀 报警监听服务启动: {string.Join(",", listener.Prefixes)}");

                while (isRunning)
                {
                    try
                    {
                        var context = listener.GetContext();
                        ProcessRequest(context);
                    }
                    catch (HttpListenerException)
                    {
                        if (!isRunning) break;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "❌ 服务异常");
                    }
                }
            }
            finally
            {
                listener.Close();
                logger.Info("⏹️ 服务已关闭");
            }
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            listener.Stop();
            if (serverThread != null && serverThread.IsAlive)
            {
                serverThread.Join();
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                if (request.HttpMethod == "POST")
                {
                    string contentType = request.ContentType ?? "";
                    logger.Info($"\n📡 收到报警 POST {request.Url.AbsolutePath}");
                    logger.Info($"📜 Content-Type: {contentType}");

                    if (contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleMultipart(request);
                    }
                    else if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                        string xmlText = reader.ReadToEnd();
                        ParseAlarmXml(xmlText);
                    }
                    else
                    {
                        logger.Warn("⚠️ 未识别的 Content-Type: " + contentType);
                    }

                    byte[] okMsg = Encoding.UTF8.GetBytes("OK");
                    response.StatusCode = 200;
                    response.OutputStream.Write(okMsg, 0, okMsg.Length);
                    response.Close();
                }
                else
                {
                    response.StatusCode = 405;
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "❌ 报警处理异常");
                try
                {
                    context.Response.StatusCode = 500;
                    byte[] errMsg = Encoding.UTF8.GetBytes("ERROR");
                    context.Response.OutputStream.Write(errMsg, 0, errMsg.Length);
                    context.Response.Close();
                }
                catch { }
            }
        }

        private void HandleMultipart(HttpListenerRequest request)
        {
            try
            {
                var parser = MultipartFormDataParser.Parse(request.InputStream, Encoding.UTF8);

                foreach (var file in parser.Files)
                {
                    string filename = file.FileName;
                    string saveName = filename;

                    //using (var fs = new FileStream(saveName, FileMode.Create, FileAccess.Write))
                    //{
                    //    file.Data.CopyTo(fs);
                    //}

                    logger.Info($"🖼️ 收到文件: {filename}, 大小: {file.Data.Length} 字节");

                    if (filename.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        file.Data.Position = 0;
                        using var reader = new StreamReader(file.Data, Encoding.UTF8);
                        string xmlText = reader.ReadToEnd();
                        ParseAlarmXml(xmlText);
                    }
                }

                foreach (var param in parser.Parameters)
                {
                    logger.Debug($"📌 字段: {param.Name} = {param.Data}");
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "⚠️ multipart 报文解析失败");
            }
        }

        private void ParseAlarmXml(string xmlText)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xmlText);

                XmlNamespaceManager nsMgr = new XmlNamespaceManager(doc.NameTable);
                string ns = doc.DocumentElement.NamespaceURI;
                nsMgr.AddNamespace("hk", ns);

                string GetText(string xpath)
                {
                    var node = doc.SelectSingleNode(xpath, nsMgr);
                    return node?.InnerText ?? "";
                }

                // 基础字段
                string ipAddress = GetText("//hk:ipAddress");
                string ipv4Address = GetText("//hk:ipV4Address");
                string portNo = GetText("//hk:portNo");
                string protocol = GetText("//hk:protocol");
                string macAddress = GetText("//hk:macAddress");
                string channelId = GetText("//hk:channelID");
                string channelName = GetText("//hk:channelName");
                string dateTime = GetText("//hk:dateTime");
                string eventType = GetText("//hk:eventType");
                string eventState = GetText("//hk:eventState");
                string eventDesc = GetText("//hk:eventDescription");
                string activePostCount = GetText("//hk:activePostCount");
                string alarmType = GetText("//hk:AudioExceptionDetection/hk:alarmType");

                // 资源信息
                string resourcesContentType = GetText("//hk:ResourcesName/hk:resourcesContentType");
                string resourcesContent = GetText("//hk:ResourcesName/hk:resourcesContent");
                string resourcesFormatType = GetText("//hk:ResourcesName/hk:resourcesFormatType");
                string pictureHeight = GetText("//hk:ResourcesName/hk:pictureResolution/hk:height");
                string pictureWidth = GetText("//hk:ResourcesName/hk:pictureResolution/hk:width");

                logger.Info("🔔 报警详情：");
                logger.Info($"  - 类型: {eventType}");
                logger.Info($"  - 描述: {eventDesc}");
                logger.Info($"  - 状态: {eventState}");
                logger.Info($"  - 时间: {dateTime}");
                logger.Info($"  - 通道: {channelId} ({channelName})");
                logger.Info($"  - 设备IP: {ipAddress ?? ipv4Address}:{portNo} [{protocol}]");
                logger.Info($"  - MAC地址: {macAddress}");
                logger.Info($"  - 上报次数: {activePostCount}");
                logger.Info($"  - 报警子类型: {alarmType}");
                logger.Info($"  - 图片格式: {resourcesFormatType}, 尺寸: {pictureWidth}x{pictureHeight}");

                AlarmReceived?.Invoke(this, new AlarmEventArgs
                {
                    EventType = eventType,
                    EventDescription = eventDesc,
                    EventState = eventState,
                    DateTime = dateTime,
                    ChannelId = channelId,
                    ChannelName = channelName,
                    DeviceIp = (ipAddress ?? ipv4Address) + ":" + portNo,
                    MacAddress = macAddress,
                    ActivePostCount = activePostCount,
                    AlarmType = alarmType,
                    ResourcesContentType = resourcesContentType,
                    ResourcesContent = resourcesContent,
                    ResourcesFormatType = resourcesFormatType,
                    PictureHeight = pictureHeight,
                    PictureWidth = pictureWidth,
                    RawXml = xmlText
                });

            }
            catch (Exception ex)
            {
                logger.Error(ex, "⚠️ XML解析失败");
                logger.Debug("原始内容：\n" + xmlText);
            }
        }
    }
}
