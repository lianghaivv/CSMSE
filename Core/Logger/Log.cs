using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using log4net;

namespace Core.Logger
{
    /// <summary>
    /// 记日志器
    /// </summary>
    public interface ILogWriter
    {
        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="messageType">日志类型</param>
        /// <param name="ex">异常</param>
        /// <param name="type">配置类型</param>
        void Write(string message, MessageType messageType, Type type, Exception ex);
    }

    /// <summary>
    /// 基于log4net实现日志器
    /// </summary>
    public class Log4NetLogWriter : ILogWriter
    {
        #region "log4net日志记录对象"

        //日志器
        private static Dictionary<RuntimeTypeHandle, ILog> _Log = new Dictionary<RuntimeTypeHandle, ILog>();
        /// <summary>
        /// 获取日志记录器
        /// </summary>
        private static ILog GetLogger(Type type)
        {
            ILog log = null;
            if (!_Log.TryGetValue(type.TypeHandle, out log))
            {
                lock (_Log)
                {
                    if (!_Log.TryGetValue(type.TypeHandle, out log))
                    {
                        log = LogManager.GetLogger(type);
                        _Log.Add(type.TypeHandle, log);
                    }
                }
            }

            //日志记录器
            return log;
        }

        #endregion

        #region ILogger Members

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="messageType">日志类型</param>
        /// <param name="ex">异常</param>
        /// <param name="type">配置类型</param>
        public void Write(string message, MessageType messageType, Type type, Exception ex)
        {
            ILog log = GetLogger(type); //LogManager.GetLogger(type);
            switch (messageType)
            {
                case MessageType.Debug: if (log.IsDebugEnabled) { log.Debug(message, ex); } break;
                case MessageType.Info: if (log.IsInfoEnabled) { log.Info(message, ex); } break;
                case MessageType.Warn: if (log.IsWarnEnabled) { log.Warn(message, ex); } break;
                case MessageType.Error: if (log.IsErrorEnabled) { log.Error(message, ex); } break;
                case MessageType.Fatal: if (log.IsFatalEnabled) { log.Fatal(message, ex); } break;
            }
            return;
        }

        #endregion
    }

    /// <summary>
    /// 写日志
    /// </summary>
    public static class Log
    {
        #region "日志记录器"

        //日志器
        private static Dictionary<RuntimeTypeHandle, ILogWriter> _Logger = new Dictionary<RuntimeTypeHandle, ILogWriter>();
        /// <summary>
        /// 获取日志记录器
        /// </summary>
        private static ILogWriter GetLogWriter(Type type)
        {
            ILogWriter logger = null;
            if (!_Logger.TryGetValue(type.TypeHandle, out logger))
            {
                lock (_Logger)
                {
                    if (!_Logger.TryGetValue(type.TypeHandle, out logger))
                    {
                        string loggerType = ConfigurationManager.AppSettings["LogWriterType"];
                        if (string.IsNullOrEmpty(loggerType))
                        { logger = new Log4NetLogWriter(); }
                        else
                        { logger = (ILogWriter)Activator.CreateInstance(Type.GetType(loggerType)); }
                        if (logger != null)
                        { _Logger.Add(type.TypeHandle, logger); }
                    }
                }
            }

            //日志记录器
            return logger;
        }

        #endregion

        #region "日志记录方法"

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="messageType">日志类型</param>
        public static void Write(string message, MessageType messageType)
        {
            Write(message, messageType, Type.GetType("System.Object"), null);
            return;
        }

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="messageType">日志类型</param>
        /// <param name="type">配置类型</param>
        public static void Write(string message, MessageType messageType, Type type)
        {
            Write(message, messageType, type, null);
            return;
        }

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="messageType">日志类型</param>
        /// <param name="ex">异常</param>
        /// <param name="type">配置类型</param>
        public static void Write(string message, MessageType messageType, Type type, Exception ex)
        {
            ILogWriter writer = GetLogWriter(type);
            writer.Write(message, messageType, type, ex);
            return;
        }

        /// <summary>
        /// 断言
        /// </summary>
        /// <param name="condition">条件</param>
        /// <param name="message">日志信息</param>
        public static void Assert(bool condition, string message)
        {
            Assert(condition, message, Type.GetType("System.Object"));
        }

        /// <summary>
        /// 断言
        /// </summary>
        /// <param name="condition">条件</param>
        /// <param name="message">日志信息</param>
        /// <param name="type">日志类型</param>
        public static void Assert(bool condition, string message, Type type)
        {
            if (!condition)
            {
                Write(message, MessageType.Info, type, null);
            }
        }

        #endregion
    }

    /// <summary>
    /// 日志类型
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// 调试
        /// </summary>
        Debug,
        /// <summary>
        /// 信息
        /// </summary>
        Info,
        /// <summary>
        /// 警告
        /// </summary>
        Warn,
        /// <summary>
        /// 错误
        /// </summary>
        Error,
        /// <summary>
        /// 致命错误
        /// </summary>
        Fatal
    }
}
