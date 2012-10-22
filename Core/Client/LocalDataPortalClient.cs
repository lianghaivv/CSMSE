using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data;

namespace Core.Client
{
    /// <summary>
    /// 本地数据入口客户端
    /// </summary>
    public class LocalDataPortalClient : IDataPortalClient
    {
        #region IDataPortalClient Members

        public bool IsServerRemote
        {
            get { return false; }
        }

        #endregion

        #region IDataPortalServer Members

        public object Get(Type objectType, object primaryKey)
        {
            IDataAccess dao = DataAccessFactory.Create(objectType);
            return dao.Get(primaryKey);
        }

        public object[] Query(Type objectType, string sql)
        {
            IDataAccess dao = DataAccessFactory.Create(objectType);
            return dao.Query(sql);
        }

        public int Insert(object obj)
        {
            IDataAccess dao = DataAccessFactory.Create(obj.GetType());
            return dao.Insert(obj);
        }

        public int Update(object obj)
        {
            IDataAccess dao = DataAccessFactory.Create(obj.GetType());
            return dao.Update(obj);
        }

        public int Delete(object obj)
        {
            IDataAccess dao = DataAccessFactory.Create(obj.GetType());
            return dao.Delete(obj);
        }

        #endregion
    }
}
