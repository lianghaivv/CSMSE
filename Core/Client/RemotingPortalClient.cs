using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Http;
using System.Text;
using Core.Data;

namespace Core.Client
{
    /// <summary>
    /// .net remoting数据入口服务端
    /// </summary>
    public class RemotingPortalClient : IDataPortalClient
    {
        static RemotingPortalClient()
        {
            Hashtable properties = new Hashtable();
            properties["name"] = "HttpBinary";
            BinaryClientFormatterSinkProvider formatter = new BinaryClientFormatterSinkProvider();
            HttpChannel channel = new HttpChannel(properties, formatter, null);
            ChannelServices.RegisterChannel(channel, EncryptChannel);

            //HttpChannel channel = new HttpChannel();
            //ChannelServices.RegisterChannel(channel, EncryptChannel);
        }

        #region IDataPortalClient Members

        public bool IsServerRemote
        {
            get { return true; }
        }

        #endregion

        #region IDataPortalServer Members

        private Server.IDataPortalServer _portal;
        /// <summary>
        /// 获取Portal对象
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
        private Server.IDataPortalServer GetPortal(Type objectType)
        {
            if (_portal == null)
            {
                string url = string.Empty;
                DataPortalSettingAttribute dpsa = (DataPortalSettingAttribute)Attribute.GetCustomAttribute(objectType, typeof(DataPortalSettingAttribute), false);
                if (dpsa != null)
                {
                    url = ConfigurationManager.AppSettings[dpsa.PortalUrlSetting];
                }
                else
                {
                    url = ConfigurationManager.AppSettings["RemotingPortalUrl"];
                }
                if (string.IsNullOrEmpty(url))
                {
                    throw new ConfigurationErrorsException("Remoting Portal Url Setting Error: Type:" + objectType.ToString());
                }
                //get dataportal
                _portal = (Server.IDataPortalServer)Activator.GetObject(typeof(Server.RemotingPortal), url);
            }
            return _portal;
        }

        private static bool EncryptChannel
        {
            get
            {
                return (ConfigurationManager.AppSettings["RemotingPortalEncrypt"] == "true");
            }
        }

        public object Get(Type objectType, object primaryKey)
        {
            return GetPortal(objectType).Get(objectType, primaryKey);
        }

        public object[] Query(Type objectType, string sql)
        {
            return GetPortal(objectType).Query(objectType, sql);
        }

        public int Insert(object obj)
        {
            return GetPortal(obj.GetType()).Insert(obj);
        }

        public int Update(object obj)
        {
            return GetPortal(obj.GetType()).Update(obj);
        }

        public int Delete(object obj)
        {
            return GetPortal(obj.GetType()).Delete(obj);
        }

        #endregion
    }
}
