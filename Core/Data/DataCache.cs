using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Data.Common;
using System.Web;
using System.Web.Caching;

namespace Core.Data
{
    /// <summary>
    /// 缓存
    /// </summary>
    public interface ICache
    {
        object Get(string key);
        object[] Get(string[] keys);
        void Insert(string key, object value);
        void Insert(string key, object value, DateTime expiry);
        void Remove(string key);
    }

    /// <summary>
    /// .NET自带的Cache
    /// </summary>
    public class DotNetCache : ICache
    {
        private static System.Web.Caching.Cache cache = System.Web.HttpRuntime.Cache;

        #region ICache Members

        /// <summary>
        /// 从缓存中获取对象
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public object Get(string key)
        {
            return cache.Get(key);
        }

        /// <summary>
        /// 从缓存中批量获取对象
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public object[] Get(string[] keys)
        {
            List<object> list = new List<object>();
            foreach (string item in keys)
            {
                object o = cache.Get(item);
                list.Add(o);
            }
            return list.ToArray();
        }

        /// <summary>
        /// 将对象插入到缓存中
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Insert(string key, object value)
        {
            cache.Insert(key, value, null, DateTime.Now.AddHours(1), Cache.NoSlidingExpiration, CacheItemPriority.High, null);
        }

        /// <summary>
        /// 将对象插入到缓存中
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expiry"></param>
        public void Insert(string key, object value, DateTime expiry)
        {
            cache.Insert(key, value, null, expiry, Cache.NoSlidingExpiration, CacheItemPriority.High, null);
        }

        /// <summary>
        /// 将对象从缓存中移除
        /// </summary>
        /// <param name="key"></param>
        public void Remove(string key)
        {
            cache.Remove(key);
        }

        ////处理部分缓存过期;清理相关联的缓存
        //private void RemovedCallback(string key, object value, CacheItemRemovedReason reason)
        //{
        //    //remove query relation
        //    string relationKey = string.Format("S:{0}:{1}", value.GetType(), key);
        //    List<string> lstRelation = cache.Get(relationKey) as List<string>;
        //    if (lstRelation != null && lstRelation.Count > 0)
        //    {
        //        string[] relations = new string[lstRelation.Count]; // copy for remove
        //        lstRelation.CopyTo(0, relations, 0, relations.Length);
        //        foreach (string item in relations)
        //        {
        //            if (!string.IsNullOrEmpty(item))
        //            {
        //                cache.Remove(item);
        //            }
        //            else
        //            { }
        //        }
        //    }
        //}

        #endregion
    }

    ///// <summary>
    ///// memcache分布式缓存
    ///// </summary>
    //public class MemCache : ICache
    //{
    //    private static BeIT.MemCached.MemcachedClient cache = BeIT.MemCached.MemcachedClient.GetInstance("MyMemCache");

    //    #region ICache Members

    //    public object Get(string key)
    //    {
    //        return cache.Get(key);
    //    }

    //    public object[] Get(string[] keys)
    //    {
    //        return cache.Get(keys);
    //    }

    //    public void Insert(string key, object value)
    //    {
    //        cache.Set(key, value, DateTime.Now.AddHours(1));
    //    }

    //    public void Insert(string key, object value, DateTime expiry)
    //    {
    //        cache.Set(key, value, expiry);
    //    }

    //    public void Remove(string key)
    //    {
    //        cache.Delete(key);
    //    }

    //    #endregion
    //}

    /// <summary>
    /// 数据缓存
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal abstract class DataCache<C, M>
        where C : DataCache<C, M>, new()
        where M : DataModel
    {
        #region "虚方法和属性"

        //缓存过期时间
        private static int _CacheExpiry = 3600;
        private static ICache _Cache = null;

        /// <summary>
        /// Cache
        /// </summary>
        public virtual ICache Cache
        {
            get
            {
                if (_Cache == null)
                {
                    string dataAccessCache = ConfigurationManager.AppSettings["DataAccessCacheType"];
                    if (string.IsNullOrEmpty(dataAccessCache))
                    { _Cache = new DotNetCache(); }
                    else
                    { _Cache = (ICache)Activator.CreateInstance(Type.GetType(dataAccessCache)); }
                }
                return _Cache;
            }
        }

        /// <summary>
        /// 缓存有效时间
        /// </summary>
        public virtual int CacheExpiry
        {
            get { return _CacheExpiry; }
            set { _CacheExpiry = value; }
        }

        protected virtual string GetTableCacheKey()
        {
            return string.Format("T:{0}:{1}", typeof(M));
        }

        /// <summary>
        /// 获取SQL查询的缓存Key
        /// </summary>
        /// <param name="type"></param>
        /// <param name="sql"></param>
        /// <returns></returns>
        protected virtual string GetSqlCacheKey(string sql)
        {
            return string.Format("S:{0}:{1}", typeof(M), sql.ToLower().Replace(" ", "_"));
        }

        /// <summary>
        /// 获取对象关联的缓存Key
        /// </summary>
        /// <param name="type"></param>
        /// <param name="uid"></param>
        /// <returns></returns>
        protected virtual string GetRelationKey(string uid)
        {
            return string.Format("R:{0}:{1}", typeof(M), uid.ToLower());
        }

        /// <summary>
        /// 获取实体的Key
        /// </summary>
        protected virtual string GetModelCacheKey(string uid)
        {
            return uid ?? string.Empty;
        }

        /// <summary>
        /// 根据主键,从缓存中获取对象
        /// </summary>
        public M Get(string uid)
        {
            return Cache.Get(GetModelCacheKey(uid)) as M;
        }

        #endregion

        #region "实例方法"

        /// <summary>
        /// 根据列值,从缓存中获取对象
        /// </summary>
        public List<M> Query(string sql)
        {
            List<M> list = null;
            string sqlKey = GetSqlCacheKey(sql);
            List<string> lstUid = Cache.Get(sqlKey) as List<string>;
            if (lstUid != null)
            {
                list = new List<M>();
                object[] models = Cache.Get(lstUid.ToArray());
                foreach (object item in models)
                {
                    M m = item as M;
                    if (m != null)
                    {
                        list.Add(item as M);
                    }
                    else
                    {
                        //只要其中有一个为null;就认为该缓存不完整
                        Cache.Remove(sqlKey);
                        return null;
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 将对象插入到缓存中
        /// </summary>
        public void Insert(M value)
        {
            Cache.Insert(GetModelCacheKey(value.UID), value, DateTime.Now.AddSeconds(CacheExpiry));
        }

        /// <summary>
        /// 将对象插入到缓存中
        /// </summary>
        public void Insert(string sql, List<M> list)
        {
            string sqlKey = GetSqlCacheKey(sql);
            List<string> lstKey = new List<string>();
            foreach (M item in list)
            {
                //field-value ==> uid cache item list
                lstKey.Add(GetModelCacheKey(item.UID));

                //insert an object into cache
                Cache.Insert(GetModelCacheKey(item.UID), item, DateTime.Now.AddSeconds(CacheExpiry));

                //insert objects & field value relation
                string relationKey = GetRelationKey(item.UID);
                List<string> lstRelation = Cache.Get(relationKey) as List<string>;
                if (lstRelation == null)
                { lstRelation = new List<string>(); }
                if (!lstRelation.Contains(sqlKey))
                { lstRelation.Add(sqlKey); }
                Cache.Insert(relationKey, lstRelation, DateTime.Now.AddSeconds(CacheExpiry));
            }
            //insert the query relation into cache
            Cache.Insert(sqlKey, lstKey, DateTime.Now.AddSeconds(CacheExpiry));
        }

        /// <summary>
        /// 清理受insert/delete原因影响的缓存数据
        /// </summary>
        /// <param name="value"></param>
        public void RemoveRelated(M value)
        {
            TableInfo ti = TableMapper.GetTableInfo(typeof(M));
            //清全选缓存
            string sql = string.Format(ti.QueryAllSqlFormat, ti.Name);
            Cache.Remove(GetSqlCacheKey(sql));

            //清根据字段查询缓存
            foreach (ColumnInfo ci in ti.Column)
            {
                sql = string.Format(ti.QueryByFieldSqlFormat, ti.Name, ci.ColumnName, ci.Getter.Invoke(value));
                Cache.Remove(GetSqlCacheKey(sql));
            }
        }

        /// <summary>
        /// 将对象从缓存中清除
        /// </summary>
        /// <param name="value"></param>
        public void Remove(M value)
        {
            //remove item
            Cache.Remove(GetModelCacheKey(value.UID));

            //remove query relation
            string relationKey = GetRelationKey(value.UID);
            List<string> lstRelation = Cache.Get(relationKey) as List<string>;
            if (lstRelation != null)
            {
                foreach (string item in lstRelation)
                {
                    if (!string.IsNullOrEmpty(item))
                    {
                        Cache.Remove(item);
                    }
                    else
                    { }
                }
            }

            //remove relation
            Cache.Remove(relationKey);
        }

        /// <summary>
        /// 更新缓存中的对象
        /// </summary>
        /// <param name="value"></param>
        public void Update(M value)
        {
            Remove(value);
            Insert(value);

            //fix bug.更新某一字段值后,通过该字段的新值的查询,缓存中数据不正确的情况
            TableInfo ti = TableMapper.GetTableInfo(typeof(M));            
            foreach (ColumnInfo ci in ti.Column)
            {
                string sql = string.Format(ti.QueryByFieldSqlFormat, ti.Name, ci.ColumnName, ci.Getter.Invoke(value));
                Cache.Remove(GetSqlCacheKey(sql));
            }
        }

        #endregion

        #region "静态方法"

        /// <summary>
        /// 查询缓存项
        /// </summary>
        public static M GetFromCache(string uid)
        {
            C c = new C();
            return c.Get(uid);
        }

        /// <summary>
        /// 查询缓存项
        /// </summary>
        public static List<M> QueryFromCache(string sql)
        {
            C c = new C();
            return c.Query(sql);
        }

        /// <summary>
        /// 将对象插入到缓存中
        /// </summary>
        public static void InsertToCache(M value)
        {
            C c = new C();
            c.Insert(value);
        }

        /// <summary>
        /// 将对象插入到缓存中
        /// </summary>
        public static void InsertToCache(string sql, List<M> list)
        {
            C c = new C();
            c.Insert(sql, list);
        }

        /// <summary>
        /// 将对象从缓存中删除
        /// </summary>
        public static void RemoveFromCache(M value)
        {
            C c = new C();
            c.Remove(value);
        }

        /// <summary>
        /// 清理与插入相关联的缓存
        /// </summary>
        /// <param name="value"></param>
        public static void RemoveRelatedCache(M value)
        {
            C c = new C();
            c.RemoveRelated(value);
        }

        /// <summary>
        /// 更新缓存中的对象
        /// </summary>
        /// <param name="value"></param>
        public static void UpdateToCache(M value)
        {
            C c = new C();
            c.Update(value);
        }

        #endregion
    }

    /// <summary>
    /// 数据存储层Cache
    /// </summary>
    /// <typeparam name="M"></typeparam>
    internal class DataAccessCache<M> : DataCache<DataAccessCache<M>, M> where M : DataModel
    { }

    /// <summary>
    /// 数据入口层Cache
    /// </summary>
    /// <typeparam name="M"></typeparam>
    internal class DataPortalCache<M> : DataCache<DataPortalCache<M>, M> where M : DataModel
    {
        private static ICache _Cache = null;
        /// <summary>
        /// Cache
        /// </summary>
        public override ICache Cache
        {
            get
            {
                if (_Cache == null)
                {
                    string dataAccessCache = ConfigurationManager.AppSettings["DataPortalCacheType"];
                    if (string.IsNullOrEmpty(dataAccessCache))
                    { _Cache = new DotNetCache(); }
                    else
                    { _Cache = (ICache)Activator.CreateInstance(Type.GetType(dataAccessCache)); }
                }
                return _Cache;
            }
        }

        private static int _CacheExpiry = 60;
        /// <summary>
        /// 缓存有效时间(默认60秒)
        /// </summary>
        public override int CacheExpiry
        {
            get { return _CacheExpiry; }
            set { _CacheExpiry = value; }
        }

        /// <summary>
        /// 获取SQL查询的缓存Key
        /// </summary>
        protected override string GetSqlCacheKey(string sql)
        {
            return string.Format("S:{0}:{1}:T{2}", typeof(M), sql.Replace(" ", "_"), System.Threading.Thread.CurrentThread.ManagedThreadId);
        }

        /// <summary>
        /// 获取对象关联的缓存Key
        /// </summary>
        protected override string GetRelationKey(string uid)
        {
            return string.Format("R:{0}:{1}:T{2}", typeof(M), uid, System.Threading.Thread.CurrentThread.ManagedThreadId);
        }

        /// <summary>
        /// 获取实体的Key
        /// </summary>
        protected override string GetModelCacheKey(string uid)
        {
            return string.Format("{0}:T{1}", uid, System.Threading.Thread.CurrentThread.ManagedThreadId);
        }
    }
}
