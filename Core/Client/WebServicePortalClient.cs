using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Configuration;
using Core.Data;

namespace Core.Client
{
    /// <summary>
    /// Web services 数据入口
    /// </summary>
    public class WebServicePortalClient : IDataPortalClient
    {
        #region IDataPortalClient Members

        public bool IsServerRemote
        {
            get { return true; }
        }

        #endregion

        #region IDataPortalServer Members

        private WebServiceHost.WebServicePortal GetPortal(Type objectType)
        {
            string url = string.Empty;
            DataPortalSettingAttribute dpsa = (DataPortalSettingAttribute)Attribute.GetCustomAttribute(objectType, typeof(DataPortalSettingAttribute), false);
            if (dpsa != null)
            {
                url = ConfigurationManager.AppSettings[dpsa.PortalUrlSetting];
            }
            else
            {
                url = ConfigurationManager.AppSettings["WebServicePortalUrl"];
            }
            if (string.IsNullOrEmpty(url))
            {
                throw new ConfigurationErrorsException("Remoting Portal Url Setting Error: Type:" + objectType.ToString());
            }
            WebServiceHost.WebServicePortal portal = new Core.WebServiceHost.WebServicePortal();
            portal.Url = url;
            return portal;
        }

        public object Get(Type objectType, object primaryKey)
        {
            object result;
            Server.WebServicePortal.GetRequest request = new Core.Server.WebServicePortal.GetRequest();
            request.ObjectType = objectType;
            request.PrimaryKey = primaryKey;

            using (WebServiceHost.WebServicePortal portal = GetPortal(objectType))
            {
                byte[] rd = Serialize(request);
                byte[] rp = portal.Get(rd);
                result = Deserialize(rp);
            }

            if (result is Exception)
            { throw (Exception)result; }

            return result;
        }

        public object[] Query(Type objectType, string sql)
        {
            object result;
            Server.WebServicePortal.QueryRequest request = new Core.Server.WebServicePortal.QueryRequest();
            request.ObjectType = objectType;
            request.Sql = sql;

            using (WebServiceHost.WebServicePortal portal = GetPortal(objectType))
            {
                byte[] rd = Serialize(request);
                byte[] rp = portal.Query(rd);
                result = Deserialize(rp);
            }

            if (result is Exception)
            { throw (Exception)result; }

            return (result as Server.WebServicePortal.QueryResponse).Result;
        }

        public int Insert(object obj)
        {
            object result;
            Server.WebServicePortal.InsertRequest request = new Core.Server.WebServicePortal.InsertRequest();
            request.ObjectType = obj.GetType();
            request.Object = obj;

            using (WebServiceHost.WebServicePortal portal = GetPortal(obj.GetType()))
            {
                byte[] rd = Serialize(request);
                byte[] rp = portal.Insert(rd);
                result = Deserialize(rp);
            }

            if (result is Exception)
            { throw (Exception)result; }

            return (result as Server.WebServicePortal.InsertResponse).Result;
        }

        public int Update(object obj)
        {
            object result;
            Server.WebServicePortal.UpdateRequest request = new Core.Server.WebServicePortal.UpdateRequest();
            request.ObjectType = obj.GetType();
            request.Object = obj;

            using (WebServiceHost.WebServicePortal portal = GetPortal(obj.GetType()))
            {
                byte[] rd = Serialize(request);
                byte[] rp = portal.Update(rd);
                result = Deserialize(rp);
            }

            if (result is Exception)
            { throw (Exception)result; }

            return (result as Server.WebServicePortal.UpdateResponse).Result;
        }

        public int Delete(object obj)
        {
            object result;
            Server.WebServicePortal.DeleteRequest request = new Core.Server.WebServicePortal.DeleteRequest();
            request.ObjectType = obj.GetType();
            request.Object = obj;

            using (WebServiceHost.WebServicePortal portal = GetPortal(obj.GetType()))
            {
                byte[] rd = Serialize(request);
                byte[] rp = portal.Delete(rd);
                result = Deserialize(rp);
            }

            if (result is Exception)
            { throw (Exception)result; }

            return (result as Server.WebServicePortal.DeleteResponse).Result;
        }

        #endregion

        #region Helper functions

        private static byte[] Serialize(object obj)
        {
            if (obj != null)
            {
                using (MemoryStream buffer = new MemoryStream())
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(buffer, obj);
                    return buffer.ToArray();
                }
            }
            return null;
        }

        private static object Deserialize(byte[] obj)
        {
            if (obj != null)
            {
                using (MemoryStream buffer = new MemoryStream(obj))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    return formatter.Deserialize(buffer);
                }
            }
            return null;
        }

        #endregion
    }
}
