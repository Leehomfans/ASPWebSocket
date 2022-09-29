using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace wb._51sole.com.Models
{
    /// <summary>
    /// WebSocket 请求类
    /// </summary>
    public class WetSocketRequestModel
    {
        /// <summary>
        /// 类型
        /// </summary>
        public string type { get; set; }
        /// <summary>
        /// 用户id
        /// </summary>
        public string uid { get; set; }
        /// <summary>
        /// 接收的uid
        /// </summary>
        public string touid { get; set; }

        /// <summary>
        /// 消息来源平台
        /// </summary>
        public string SourePlatform { get; set; } = MsgSourceType.wx.ToString();
        
        /// <summary>
        /// 消息主体内容
        /// </summary>
        public MsgModel msg { get; set; }
    }
    public enum MsgSourceType
    {
        wx,//微信
        web,//网页
        dy,//抖音
    }
    public class MsgModel
    {
        public int ID { get; set; }
        public string AppId { get; set; }
        public string MsgId { get; set; }
        public string FromUserName { get; set; }
        public int IsReply { get; set; }
        public string NickName { get; set; }
        public string MsgContent { get; set; }
        public string MsgType { get; set; }
        public string PicUrl { get; set; }
        public string CreateTime { get; set; }
    }

    public class UserInfo
    {
        public string uid { get; set; }//用户唯一标识
        public string sourcePlatform { get; set; } = "wx";//来源平台
        public string userAgent { get; set; }//浏览器标识
        public string IpAddress { get; set; }//IP地址
        public string onlineTime { get; set; } //上线时间    
    }
}