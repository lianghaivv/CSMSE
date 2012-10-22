using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Core.Data
{
    /// <summary>
    /// 数据操作工厂
    /// </summary>
    public static class DataAccessFactory
    {
        private static readonly object syncRoot = new object();
        private static Dictionary<RuntimeTypeHandle, IDataAccess> dicDataAccess = new Dictionary<RuntimeTypeHandle, IDataAccess>();

        /// <summary>
        /// 获取数据操作器
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
        public static IDataAccess Create(Type objectType)
        {
            IDataAccess dao = null;
            if (!dicDataAccess.TryGetValue(objectType.TypeHandle, out dao))
            {
                lock (syncRoot)
                {
                    if (!dicDataAccess.TryGetValue(objectType.TypeHandle, out dao))
                    {
                        dao = (IDataAccess)Activator.CreateInstance(typeof(DataAccess<>).MakeGenericType(objectType));
                        dicDataAccess.Add(objectType.TypeHandle, dao);
                    }
                }
            }
            return dao;
        }
    }
}
