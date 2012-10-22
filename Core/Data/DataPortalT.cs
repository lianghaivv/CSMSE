using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Core.Client;
using Core.Data;

namespace Core.Data
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DataPortalSettingAttribute : Attribute
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="appSettingName">应用程序设置项名称</param>
        public DataPortalSettingAttribute(string portalClientSetting, string portalUrlSetting)
        {
            this.PortalClientSetting = string.IsNullOrEmpty(portalClientSetting) ? "DataPortalClient" : portalClientSetting;
            this.PortalUrlSetting = string.IsNullOrEmpty(portalUrlSetting) ? "DataPortalUrl" : portalUrlSetting;
        }

        /// <summary>
        /// 应用程序设置项名称
        /// </summary>
        public string PortalClientSetting { get; private set; }
        public string PortalUrlSetting { get; private set; }
    }

    /// <summary>
    /// 数据入口的通用类
    /// </summary>
    public class DataPortalT<M> where M : DataModel
    {
        #region "构造函数"

        public DataPortalT()
            : this(GetDataPortalSettingName())
        { }
        public DataPortalT(string dataPortalProviderAppSettingName)
            : this(GetDataPortalProviderType(dataPortalProviderAppSettingName))
        { }
        public DataPortalT(Type dataPortalType)
        {
            if (_DataPortal == null)
            {
                lock (_SyncRoot)
                {
                    if (_DataPortal == null)
                    { _DataPortal = (IDataPortalClient)Activator.CreateInstance(dataPortalType); }

                    //远程数据入口,启用一级缓存
                    _IsEnableCache = (!Attribute.IsDefined(typeof(M), typeof(CacheDisableAttribute), false))
                                  && ((ConfigurationManager.AppSettings["DataPortalCacheEnable"] ?? "true") == "true")
                                  && _DataPortal.IsServerRemote;

                    //数据库表名
                    TableAttribute ta = (TableAttribute)Attribute.GetCustomAttribute(typeof(M), typeof(TableAttribute), false);
                    if (ta != null)
                    {
                        _TableName = ta.Name;
                    }
                    else
                    {
                        string name = typeof(M).Name;
                        if (name.EndsWith("Info", StringComparison.OrdinalIgnoreCase))
                        {
                            _TableName = name;
                        }
                        else
                        {
                            _TableName = name + "Info";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取数据入口设置名称
        /// </summary>
        /// <returns></returns>
        private static string GetDataPortalSettingName()
        {
            if (string.IsNullOrEmpty(_ProviderAppSettingName))
            {
                DataPortalSettingAttribute dsa = (DataPortalSettingAttribute)Attribute.GetCustomAttribute(typeof(M), typeof(DataPortalSettingAttribute), false);
                _ProviderAppSettingName = (dsa == null) ? "DataPortalClient" : dsa.PortalClientSetting;
            }
            return _ProviderAppSettingName;
        }

        /// <summary>
        /// 获取数据入口操作器类型
        /// </summary>
        private static Type GetDataPortalProviderType(string dataPortalProviderAppSettingName)
        {
            if (_DataPortal == null)
            {
                string provider = ConfigurationManager.AppSettings[dataPortalProviderAppSettingName];
                return string.IsNullOrEmpty(provider) ? typeof(LocalDataPortalClient) : Type.GetType(provider);
            }
            else
            {
                return _DataPortal.GetType();
            }
        }

        #endregion

        #region "属性"

        protected static readonly object _SyncRoot = new object();
        protected static IDataPortalClient _DataPortal = null;
        protected static string _ProviderAppSettingName = string.Empty;
        protected static string _TableName = string.Empty;
        protected static bool _IsEnableCache = false;

        #endregion

        #region "实例方法"

        protected virtual void OnInsertBegin(M entity) { }
        protected virtual void OnInsertCompleted(M entity) { }
        protected virtual void OnDeleteBegin(M entity) { }
        protected virtual void OnDeleteCompleted(M entity) { }
        protected virtual void OnUpdateBegin(M entity) { }
        protected virtual void OnUpdateCompleted(M entity) { }
        protected virtual void OnQueryBegin() { }
        protected virtual void OnQueryCompleted(List<M> entities) { }

        /// <summary>
        /// 将对象插入到数据库中
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public int Insert(M entity)
        {
            //on insert begin
            OnInsertBegin(entity);

            //insert to database
            int result = _DataPortal.Insert(entity);

            //on insert completed
            OnInsertCompleted(entity);

            if (_IsEnableCache)
            {
                //update cache
                DataPortalCache<M>.InsertToCache(entity);
                DataPortalCache<M>.RemoveRelatedCache(entity);
            }

            return result;
        }

        /// <summary>
        /// 将对象从数据库中删除
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public int Delete(M entity)
        {
            //on delete begin
            OnDeleteBegin(entity);

            //update to database
            int result = _DataPortal.Delete(entity);

            //on delete completed
            OnDeleteCompleted(entity);

            if (_IsEnableCache)
            {
                //update cache
                DataPortalCache<M>.RemoveFromCache(entity);
            }

            return result;
        }

        /// <summary>
        /// 将对象更新到数据库中
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public int Update(M entity)
        {
            //on update begin
            OnUpdateBegin(entity);

            //update to database
            int result = _DataPortal.Update(entity);

            //on update completed
            OnUpdateCompleted(entity);

            if (_IsEnableCache)
            {
                //update cache
                DataPortalCache<M>.UpdateToCache(entity);
            }

            return result;
        }

        /// <summary>
        /// 根据主键进行查询对象
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public M Get(string uid)
        {
            M model = null;
            //get form cache
            if (_IsEnableCache)
            {
                model = DataPortalCache<M>.GetFromCache(uid);
            }
            //get from database
            if (model == null)
            {
                //on query begin
                OnQueryBegin();

                //get model by primary
                model = (M)_DataPortal.Get(typeof(M), uid);

                //on query completed
                List<M> list = new List<M>();
                list.Add(model);
                OnQueryCompleted(list);

                //insert to cache
                if (_IsEnableCache && model != null)
                { DataPortalCache<M>.InsertToCache(model); }
            }

            return model;
        }

        /// <summary>
        /// 根据SQL查询对象
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public List<M> Query(string sql)
        {
            List<M> list = null;
            //get form cache
            if (_IsEnableCache)
            {
                list = DataPortalCache<M>.QueryFromCache(sql);
            }

            if (list == null)
            {
                //on query begin
                OnQueryBegin();

                //get from database
                object[] query = _DataPortal.Query(typeof(M), sql);

                list = new List<M>();
                foreach (object item in query)
                { list.Add(item as M); }

                //on query completed
                OnQueryCompleted(list);

                //set to cache
                if (_IsEnableCache && list != null)
                {
                    DataPortalCache<M>.InsertToCache(sql, list);
                }
            }
            return list;
        }

        /// <summary>
        /// 根据字段时行查询
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public List<M> QueryByField(string field, object value)
        {
            TableInfo ti = TableMapper.GetTableInfo(typeof(M));
            return Query(string.Format(ti.QueryByFieldSqlFormat, _TableName, field, value));
        }

        /// <summary>
        /// 根据字段时行查询
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public List<M> QueryAll()
        {
            TableInfo ti = TableMapper.GetTableInfo(typeof(M));
            return Query(string.Format(ti.QueryAllSqlFormat, _TableName));
        }

        #endregion
    }

    /// <summary>
    /// 数据入口的基类
    /// </summary>
    public abstract class DataPortal<D, M> : DataPortalT<M>
        where D : DataPortal<D, M>, new()
        where M : DataModel, new()
    {
        public DataPortal()
            : base()
        { }
        public DataPortal(string dataPortalProviderAppSettingName)
            : base(dataPortalProviderAppSettingName)
        { }
        public DataPortal(Type dataPortalType)
            : base(dataPortalType)
        { }

        #region "静态方法"

        /// <summary>
        /// 将对象插入到数据库中
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static int InsertToDb(M entity)
        {
            D d = new D();
            return d.Insert(entity);
        }

        /// <summary>
        /// 将对象从数据库中删除
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static int DeleteFromDb(M entity)
        {
            D d = new D();
            return d.Delete(entity);
        }

        /// <summary>
        /// 将对象更新到数据库中
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static int UpdateToDb(M entity)
        {
            D d = new D();
            return d.Update(entity);
        }


        /// <summary>
        /// 根据主键进行查询对象
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public static M GetModel(string uid)
        {
            D d = new D();
            return d.Get(uid);
        }

        /// <summary>
        /// 根据字段获取
        /// </summary>
        /// <param name="filed"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static M GetModelByField(string field, object value)
        {
            D d = new D();
            List<M> list = d.QueryByField(field, value);
            return (list != null && list.Count > 0) ? list[0] : null;
        }

        /// <summary>
        /// 根据字段获取
        /// </summary>
        /// <param name="filed"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static M GetModelByField(string field, object value, Predicate<M> match)
        {
            D d = new D();
            List<M> list = d.QueryByField(field, value);
            return (list != null && list.Count > 0) ? list.Find(match) : null;
        }

        /// <summary>
        /// 根据字段获取
        /// </summary>
        /// <param name="filed"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static List<M> GetModels(string field, object value)
        {
            D d = new D();
            return d.QueryByField(field, value);
        }

        /// <summary>
        /// 根据字段获取
        /// </summary>
        /// <param name="filed"></param>
        /// <param name="value"></param>
        /// <param name="match"></param>
        /// <returns></returns>
        public static List<M> GetModels(string field, object value, Predicate<M> match)
        {
            D d = new D();
            List<M> list = d.QueryByField(field, value);
            return list.FindAll(match);
        }

        /// <summary>
        /// 获取所有对象
        /// </summary>
        /// <returns></returns>
        public static List<M> GetAllModels()
        {
            D d = new D();
            return d.QueryAll();
        }

        #endregion
    }
}
