using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Web.Caching;
using System.Xml;
using Core.Logger;

namespace Core.Data
{
    /// <summary>
    /// 禁止使用缓存标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CacheDisableAttribute : Attribute
    { }

    /// <summary>
    /// 数据库连接串相关设置标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ConnectionSettingAttribute : Attribute
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connectionStringName">数据库连接串名称</param>
        public ConnectionSettingAttribute(string connectionStringName)
        {
            this.ConnectionStringName = string.IsNullOrEmpty(connectionStringName) ? "DbConnectionString" : connectionStringName;
        }

        /// <summary>
        /// 数据库连接串名称
        /// </summary>
        public string ConnectionStringName { get; private set; }
    }

    /// <summary>
    /// 查询相关设置标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class QuerySettingAttribute : Attribute
    {
        /// <summary>
        /// 全选查询SQL格式
        /// </summary>
        public const string SQL_QUERY_ALL = "SELECT * FROM {0}";
        /// <summary>
        /// 根据字段查询SQL格式
        /// </summary>
        public const string SQL_QUERY_BY_FIELD = "SELECT * FROM {0} WHERE {1}='{2}'";

        public QuerySettingAttribute()
            : this(string.Empty, SQL_QUERY_ALL, SQL_QUERY_BY_FIELD)
        {
        }

        public QuerySettingAttribute(string tableAliasName, string queryAllSqlFormat, string queryByFieldSqlFormat)
        {
            TableAliasName = tableAliasName;
            QueryAllSqlFormat = queryAllSqlFormat;
            QueryByFieldSqlFormat = queryByFieldSqlFormat;            
        }

        /// <summary>
        /// 表别名
        /// </summary>
        public string TableAliasName { get; set; }
        /// <summary>
        /// 全选SQL格式
        /// </summary>
        public string QueryAllSqlFormat { get; set; }
        /// <summary>
        /// 根据字段查询SQL格式
        /// </summary>
        public string QueryByFieldSqlFormat { get; set; }
    }

    /// <summary>
    /// 数据操作
    /// </summary>
    public interface IDataAccess
    {
        /// <summary>
        /// 将对象插入到数据库中
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        int Insert(object entity);
        /// <summary>
        /// 将对象从数据库中删除
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        int Delete(object entity);
        /// <summary>
        /// 将对象更新到数据库中
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        int Update(object entity);
        /// <summary>
        /// 根据主键进行查询对象
        /// </summary>
        /// <param name="primaryKey"></param>
        /// <returns></returns>
        object Get(object primaryKey);
        /// <summary>
        /// 根据SQL查询对象
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        object[] Query(string sql);
    }

    /// <summary>
    /// 数据操作类
    /// </summary>
    /// <typeparam name="M">实体类</typeparam>
    public class DataAccess<M> : IDataAccess where M : DataModel
    {
        #region "构造函数"

        public DataAccess()
            : this(GetConnectionStringName())
        { }

        public DataAccess(string connectionStringName)
        {
            if (string.IsNullOrEmpty(connectionStringName))
            {
                if (ConfigurationManager.ConnectionStrings.Count > 0)
                {
                    connectionStringName = ConfigurationManager.ConnectionStrings[0].Name;
                }
                else
                {
                    throw new InvalidOperationException("Can't find any connection string");
                }
            }
            string providerName = "System.Data.SqlClient";
            if (ConfigurationManager.ConnectionStrings[connectionStringName] != null)
            {
                if (!string.IsNullOrEmpty(ConfigurationManager.ConnectionStrings[connectionStringName].ProviderName))
                {
                    providerName = ConfigurationManager.ConnectionStrings[connectionStringName].ProviderName;
                }
            }
            else
            {
                throw new InvalidOperationException("Can't find a connection string with the name '" + connectionStringName + "'");
            }

            _ConnectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
            _ProviderName = providerName;
        }

        public DataAccess(string connectionString, string providerName)
        {
            _ConnectionString = connectionString;
            _ProviderName = providerName;
        }

        /// <summary>
        /// 获取数据库连接串名称
        /// </summary>
        /// <returns></returns>
        private static string GetConnectionStringName()
        {
            ConnectionSettingAttribute csa = (ConnectionSettingAttribute)Attribute.GetCustomAttribute(typeof(M), typeof(ConnectionSettingAttribute), false);
            return csa == null ? "DbConnectionString" : csa.ConnectionStringName;
        }

        #endregion

        #region "属性"

        /// <summary>
        /// 是否允许缓存
        /// </summary>
        private static readonly bool _IsEnableCache = (!Attribute.IsDefined(typeof(M), typeof(CacheDisableAttribute), false)) 
                                                   && ((ConfigurationManager.AppSettings["DataAccessCacheEnable"] ?? "true") == "true");

        private DbProviderFactory _Factory = null;
        private string _ProviderName = string.Empty;
        private string _ConnectionString = string.Empty;
        //private IDbConnection _Connection = null;
        /// <summary>
        /// 数据库连接串
        /// </summary>
        protected IDbConnection Connection
        {
            get
            {
                //if (_Factory == null)
                //{ _Factory = DbProviderFactories.GetFactory(_ProviderName); }
                //IDbConnection _Connection = _Factory.CreateConnection();
                //_Connection.ConnectionString = _ConnectionString;
                //return _Connection;

                IDbConnection cnn = null;
                if (string.Compare(_ProviderName, "MySql.Data.MySqlClient", true) == 0)
                {
                    cnn = new MySql.Data.MySqlClient.MySqlConnection();
                }
                else if (string.Compare(_ProviderName, "System.Data.SQLite", true) == 0)
                {
                    cnn = new System.Data.SQLite.SQLiteConnection();
                }
                else
                {
                    if (_Factory == null)
                    { _Factory = DbProviderFactories.GetFactory(_ProviderName); }
                    cnn = _Factory.CreateConnection();
                }
                cnn.ConnectionString = _ConnectionString;

                return cnn;
            }
        }

        #endregion

        #region "实例方法"

        /// <summary>
        /// 将对象插入到数据库中
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public int Insert(M entity)
        {
            //insert to database
            int result = Connection.Insert(entity);

            if (_IsEnableCache)
            {
                //update cache
                DataAccessCache<M>.InsertToCache(entity);
                DataAccessCache<M>.RemoveRelatedCache(entity);
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
            //update to database
            int result = Connection.Delete(entity);

            if (_IsEnableCache)
            {
                //update cache
                DataAccessCache<M>.RemoveFromCache(entity);
                DataAccessCache<M>.RemoveRelatedCache(entity);
            }

            return result;
        }

        ////更新SQL
        //private static string SQL_UPDATE_BY_VERSION = string.Empty;
        //private static readonly object _SyncRoot = new object();
        /// <summary>
        /// 将对象更新到数据库中
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public int Update(M entity)
        {
            entity.UpdateTime = DateTime.Now;

            //update
            int result = Connection.Update(entity);

            if (_IsEnableCache)
            {
                //update cache
                DataAccessCache<M>.UpdateToCache(entity);
            }
            return result;

            //lock (_SyncRoot)
            //{
            //    entity.UpdateTime = DateTime.Now;
            //    entity.Ver += 1;

            //    TableInfo table = TableMapper.GetTableInfo(typeof(M));
            //    if (string.IsNullOrEmpty(SQL_UPDATE_BY_VERSION))
            //    {
            //        string prefix = DbMapper.GetParamPrefix(Connection);
            //        List<string> lstColumn = new List<string>();
            //        foreach (ColumnInfo item in table.Column)
            //        {
            //            lstColumn.Add(string.Format("{0}={1}{0}", item.ColumnName, prefix));
            //        }
            //        //bind update sql
            //        SQL_UPDATE_BY_VERSION = string.Format("UPDATE {0} SET {1} WHERE {2}={3}{2} AND Ver=({3}Ver - 1)", table.Name, string.Join(",", lstColumn.ToArray()), table.PrimaryKey.ColumnName, prefix);
            //    }

            //    //build parameter
            //    DbParameterBuilder builder = new DbParameterBuilder();
            //    object primaryValue = table.PrimaryKey.Getter(entity);
            //    builder.AddParameter(table.PrimaryKey.ColumnName, primaryValue, table.PrimaryKey.ColumnDbType, ParameterDirection.Input, null);
            //    foreach (ColumnInfo item in table.Column)
            //    {
            //        object value = item.Getter(entity);
            //        builder.AddParameter(item.ColumnName, value, item.ColumnDbType, ParameterDirection.Input, null);
            //    }

            //    //update to database
            //    int result = Connection.Execute(CommandType.Text, SQL_UPDATE_BY_VERSION, builder);
            //    //int result = Connection.Update(entity);

            //    //on excute update ERROR, write log and force update.
            //    if (result != 1)
            //    {
            //        //write log
            //        M m = Connection.Get<M>(entity.UID);
            //        System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(M));
            //        //original entity
            //        StringBuilder sbOriginal = new StringBuilder();
            //        XmlWriter xwOriginal = XmlWriter.Create(sbOriginal);
            //        xs.Serialize(xwOriginal, m);
            //        //currency entity
            //        StringBuilder sbCurrency = new StringBuilder();
            //        XmlWriter xwCurrency = XmlWriter.Create(sbCurrency);
            //        xs.Serialize(xwCurrency, entity);

            //        Log.Write(string.Format("update error.\r\n original:\r\n{0}\r\n currency:\r\n{1}\r\n", sbOriginal, sbCurrency), MessageType.Fatal, typeof(M));

            //        //force update
            //        result = Connection.Update(entity);
            //    }

            //    if (_IsEnableCache)
            //    {
            //        //update cache
            //        DataAccessCache<M>.UpdateToCache(entity);
            //    }
            //    return result;
            //}
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
                model = DataAccessCache<M>.GetFromCache(uid);
            }
            //get from database
            if (model == null)
            {
                //get model by primary
                model = Connection.Get<M>(uid);

                //insert to cache
                if (_IsEnableCache && model != null)
                { DataAccessCache<M>.InsertToCache(model); }
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
                list = DataAccessCache<M>.QueryFromCache(sql);
            }

            if (list == null || list.Count == 0)
            {
                //get from database
                list = Connection.Query<M>(CommandType.Text, sql, null);

                //set to cache
                if (_IsEnableCache && list != null)
                {
                    Action<string, List<M>> action = new Action<string, List<M>>(DataAccessCache<M>.InsertToCache);
                    //action.BeginInvoke(sql, list, null, null);
                    action.Invoke(sql, list);
                }
            }
            return list;
        }

        #endregion

        #region IDataAccess Members

        int IDataAccess.Insert(object entity)
        {
            return Insert((M)entity);
        }

        int IDataAccess.Delete(object entity)
        {
            return Delete((M)entity);
        }

        int IDataAccess.Update(object entity)
        {
            return Update((M)entity);
        }

        object IDataAccess.Get(object primaryKey)
        {
            return (M)Get((string)primaryKey);
        }

        object[] IDataAccess.Query(string sql)
        {
            return Query(sql).ToArray();
        }

        #endregion
    }
}
