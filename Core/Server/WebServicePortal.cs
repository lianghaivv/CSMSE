using System;
using System.Web.Services;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Core.Data;

namespace Core.Server
{
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    public class WebServicePortal : WebService
    {
        #region "request and response"

        [Serializable]
        public class GetRequest
        {
            public Type ObjectType { get; set; }
            public object PrimaryKey { get; set; }
        }

        [Serializable]
        public class QueryRequest
        {
            public Type ObjectType { get; set; }
            public string Sql { get; set; }
        }

        [Serializable]
        public class GetResponse
        {
            public object Result { get; set; }
        }

        [Serializable]
        public class QueryResponse
        {
            public object[] Result { get; set; }
        }

        [Serializable]
        public class InsertRequest
        {
            public Type ObjectType { get; set; }
            public object Object { get; set; }
        }

        [Serializable]
        public class InsertResponse
        {
            public int Result { get; set; }
        }

        [Serializable]
        public class UpdateRequest
        {
            public Type ObjectType { get; set; }
            public object Object { get; set; }
        }

        [Serializable]
        public class UpdateResponse
        {
            public int Result { get; set; }
        }

        [Serializable]
        public class DeleteRequest
        {
            public Type ObjectType { get; set; }
            public object Object { get; set; }
        }

        [Serializable]
        public class DeleteResponse
        {
            public int Result { get; set; }
        }

        #endregion

        #region "services"

        [WebMethod()]
        public byte[] Get(byte[] requestData)
        {
            object result = null;
            try
            {
                GetRequest request = (GetRequest)Deserialize(requestData);
                IDataAccess dao = DataAccessFactory.Create(request.ObjectType);
                result = dao.Get(request.PrimaryKey);
            }
            catch (Exception ex)
            { result = ex; }

            return Serialize(result);
        }

        [WebMethod()]
        public byte[] Query(byte[] requestData)
        {
            object result = null;
            try
            {
                QueryRequest request = (QueryRequest)Deserialize(requestData);
                IDataAccess dao = DataAccessFactory.Create(request.ObjectType);
                object[] objs = dao.Query(request.Sql);
                result = new QueryResponse() { Result = objs };
            }
            catch (Exception ex)
            { result = ex; }

            return Serialize(result);
        }

        [WebMethod()]
        public byte[] Insert(byte[] requestData)
        {
            object result = null;
            try
            {
                InsertRequest request = (InsertRequest)Deserialize(requestData);
                IDataAccess dao = DataAccessFactory.Create(request.ObjectType);
                int state = dao.Insert(request.Object);
                result = new InsertResponse() { Result = state };
            }
            catch (Exception ex)
            { result = ex; }

            return Serialize(result);
        }

        [WebMethod()]
        public byte[] Update(byte[] requestData)
        {
            object result = null;
            try
            {
                UpdateRequest request = (UpdateRequest)Deserialize(requestData);
                IDataAccess dao = DataAccessFactory.Create(request.ObjectType);
                int state = dao.Update(request.Object);
                result = new UpdateResponse() { Result = state };
            }
            catch (Exception ex)
            { result = ex; }

            return Serialize(result);
        }

        [WebMethod()]
        public byte[] Delete(byte[] requestData)
        {
            object result = null;
            try
            {
                DeleteRequest request = (DeleteRequest)Deserialize(requestData);
                IDataAccess dao = DataAccessFactory.Create(request.ObjectType);
                int state = dao.Delete(request.Object);
                result = new DeleteResponse() { Result = state };
            }
            catch (Exception ex)
            { result = ex; }

            return Serialize(result);
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
