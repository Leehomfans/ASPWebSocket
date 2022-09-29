using _51Sole.DJG.Common;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using System.Web.WebSockets;
using wb._51sole.com.Models;

namespace wb._51sole.com.Controllers
{
    /// <summary>
    ///  WebSocket通讯
    /// </summary>
    [RoutePrefix("WebSocket")]
    [AllowAnonymous]
    public class WebSocketController : ApiController
    {
        public static List<UserInfo> users = new List<UserInfo>();
        /// Socket 集合 对应 用户ID 与 WebSocket对象
        /// </summary>
        public static ConcurrentDictionary<string, WebSocket> socket_list = new ConcurrentDictionary<string, WebSocket>();
        /// <summary>
        /// get方法，判断是否是websocket请求
        /// </summary>
        /// <returns></returns>
        [Route("")]
        public HttpResponseMessage Get()
        {
            if (HttpContext.Current.IsWebSocketRequest)
            {
                HttpContext.Current.AcceptWebSocketRequest(ProcessWebsocket);
            }
            return new HttpResponseMessage(HttpStatusCode.SwitchingProtocols);
        }
        /// <summary>
        /// 异步线程处理websocket消息
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private async Task ProcessWebsocket(AspNetWebSocketContext arg)
        {
            var ipAddress = arg.UserHostAddress;
            var ua = arg.UserAgent;
            WebSocket socket = arg.WebSocket;
            string userid = string.Empty;
            while (true)
            {
                try
                {
                    ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[10240]);
                    string returnMessage = "";//返回消息             

                    WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        //释放websoket对象
                        var l = socket_list.Where(t => t.Value == socket);
                        if (l.Count() > 0)
                        {
                            foreach (var item in l)
                            {
                                WebSocket ws = null;
                                bool isSuccess = socket_list.TryRemove(item.Key, out ws);
                                if (isSuccess)
                                {
                                    lock (users)
                                    {
                                        UserInfo user = users.Where(u => u.uid == item.Key).FirstOrDefault();
                                        if (user != null)
                                        {
                                            users.Remove(user);
                                        }
                                    }
                                }
                            }
                        }
                        await socket.CloseOutputAsync(WebSocketCloseStatus.Empty, string.Empty, CancellationToken.None);//如果client发起close请求，对client进行ack   
                        break;
                    }
                    if (socket.State == WebSocketState.Connecting)
                    {

                    }
                    else if (socket.State == WebSocketState.Open)
                    {
                        try
                        {
                            string message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                            WetSocketRequestModel model = JsonConvert.DeserializeObject<WetSocketRequestModel>(message);
                            if (model.type == "open_connect")
                            {
                                string uid = model.uid;
                                string sourcePlatform = model.SourePlatform;
                                //判断用户ID是否为空，为空则初始化ID
                                if (string.IsNullOrEmpty(model.uid))
                                {
                                    uid = Guid.NewGuid().ToString().Replace("-", "");
                                    socket_list.TryAdd(uid, socket);
                                    users.Add(new UserInfo
                                    {
                                        uid = uid,
                                        sourcePlatform = sourcePlatform,
                                        userAgent = ua,
                                        IpAddress = ipAddress,
                                        onlineTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                    });
                                }
                                else
                                {
                                    if (socket_list.Where(t => t.Key == model.uid).Count() == 0)
                                    {
                                        users.Add(new UserInfo
                                        {
                                            uid = uid,
                                            sourcePlatform = sourcePlatform,
                                            userAgent = ua,
                                            IpAddress = ipAddress,
                                            onlineTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                        });
                                        socket_list.TryAdd(model.uid, socket);
                                    }
                                    else
                                    {
                                        //关闭之前的链接,重置为当前链接
                                        try
                                        {
                                            WebSocket ws = null;
                                            bool isSuccess = socket_list.TryRemove(model.uid, out ws);
                                            if (isSuccess)
                                            {
                                                if (ws != null && ws.State == WebSocketState.Open && model.SourePlatform != "web")
                                                {
                                                    returnMessage = "{\"type\":\"force_exit\",\"uid\":\"" + model.uid + "\",\"message\":\"你在其他设备登录了!\"}";
                                                    buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(returnMessage));
                                                    await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                                                    await ws.CloseOutputAsync(WebSocketCloseStatus.Empty, string.Empty, CancellationToken.None);//主动关闭连接
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.WriteLog("重置用户" + model.uid + "链接异常:" + ex.Message);
                                        }
                                        //更新用户登录信息
                                        lock (users)
                                        {
                                            UserInfo user = users.Where(u => u.uid == model.uid).FirstOrDefault();
                                            if (user != null)
                                            {
                                                user.sourcePlatform = sourcePlatform;
                                                user.userAgent = ua;
                                                user.IpAddress = ipAddress;
                                                user.onlineTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                            }
                                        }
                                        socket_list[model.uid] = socket;
                                    }
                                }
                                userid = uid;
                                //建立连接                  
                                returnMessage = "{\"type\":\"open_init\",\"uid\":\"" + uid + "\"}";
                                buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(returnMessage));
                                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                                //心跳连接
                                returnMessage = "{\"type\":\"open_health\",\"uid\":\"" + model.uid + "\",\"message\":\"连接成功!\"}";
                                buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(returnMessage));
                                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            else if (model.type == "send_msg")
                            {
                                if (!socket_list.ContainsKey(model.uid))
                                {
                                    returnMessage = "{\"type\":\"open_err\",\"uid\":\"\",\"message\":\"非法连接!\"}";
                                    buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(returnMessage));
                                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                                }
                                else
                                {
                                    //转发消息
                                    if (socket_list.ContainsKey(model.touid ?? ""))
                                    {
                                        try
                                        {
                                            WebSocket desSocket = socket_list[model.touid];
                                            if (desSocket != null && desSocket.State == WebSocketState.Open)
                                            {
                                                returnMessage = JsonConvert.SerializeObject(new WetSocketRequestModel
                                                {
                                                    type = "get_msg",
                                                    uid = model.uid,
                                                    touid = model.touid,
                                                    SourePlatform = model.SourePlatform,
                                                    msg = model.msg
                                                });
                                                buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(returnMessage));
                                                await desSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                                                Logger.WriteLog("服务端转发消息:" + returnMessage + "成功");
                                            }
                                            else
                                            {
                                                Logger.WriteLog("服务端转发消息" + returnMessage + "失败,接收端不在线!");
                                                //释放websoket对象
                                                WebSocket ws = null;
                                                bool isSuccess = socket_list.TryRemove(model.touid, out ws);
                                                if (isSuccess)
                                                {
                                                    lock (users)
                                                    {
                                                        UserInfo user = users.Where(u => u.uid == userid).FirstOrDefault();
                                                        if (user != null)
                                                        {
                                                            users.Remove(user);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Logger.WriteLog("socketlist删除元素失败");
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.WriteLog("服务端转发消息异常:" + ex.Message);
                                        }
                                    }

                                    //心跳连接
                                    returnMessage = "{\"type\":\"open_health\",\"uid\":\"" + model.uid + "\",\"message\":\"连接成功!\"}";
                                    buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(returnMessage));
                                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                                }
                            }
                            else if (model.type == "open_health")
                            {
                                //服务端发送心跳回复信息
                                returnMessage = "{\"type\":\"open_health_ack\",\"uid\":\"" + model.uid + "\",\"message\":\"服务端发送心跳!\"}";
                                buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(returnMessage));
                                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            else if (model.type == "open_close")
                            {
                                //关闭连接
                                WebSocket ws = null;
                                lock (users)
                                {
                                    UserInfo user = users.Where(u => u.uid == model.uid).FirstOrDefault();
                                    if (user != null)
                                    {
                                        users.Remove(user);
                                    }
                                }
                                socket_list.TryRemove(model.uid, out ws);
                                await socket.CloseOutputAsync(WebSocketCloseStatus.Empty, string.Empty, CancellationToken.None);//如果client发起close请求，对client进行ack
                            }
                            else
                            {
                                //其他,未知消息
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLog("异常:" + ex.Message);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLog(userid + "--非正常关闭Socket异常:" + ex.Message);
                    //释放websoket对象
                    WebSocket ws = null;
                    bool isSuccess = socket_list.TryGetValue(userid, out ws);
                    if (isSuccess && socket.Equals(ws))
                    {
                        isSuccess = socket_list.TryRemove(userid, out ws);
                        if (isSuccess)
                        {
                            lock (users)
                            {
                                UserInfo user = users.Where(u => u.uid == userid).FirstOrDefault();
                                if (user != null)
                                {
                                    users.Remove(user);
                                }
                            }
                        }
                    }
                    else
                    {
                        Logger.WriteLog("dispose old connection");
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 查询用户在线状态
        /// </summary>
        /// <param name="AccountId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("GetOnlineState")]
        public IHttpActionResult GetOnlineState(string AccountId)
        {
            return Json<dynamic>(new
            {
                code = 0,
                data = new
                {
                    IsOnline = socket_list.ContainsKey(AccountId) && socket_list[AccountId].State == WebSocketState.Open
                }
            });
        }

        /// <summary>
        /// 获取所有连接状态
        /// </summary>
        /// <param name="AccountId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("GetAllConnections")]
        public IHttpActionResult GetAllConnections()
        {
            return Json<dynamic>(new
            {
                code = 0,
                total = socket_list.Count(),
                data = socket_list
            });
        }

        [HttpGet]
        [Route("GetAllOnlineUsers")]
        public IHttpActionResult GetAllOnlineUsers(int pageIndex = 1, int pageSize = 100)
        {
            return Json<dynamic>(new
            {
                code = 0,
                total = users.Count(),
                data = users.Skip((pageIndex - 1) * pageSize).Take(pageSize)
            });
        }
    }

}
