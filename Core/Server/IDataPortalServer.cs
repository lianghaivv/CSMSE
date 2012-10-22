using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Core.Server
{
    /// <summary>
    /// 数据入口服务端接口
    /// </summary>
    public interface IDataPortalServer
    {
        /// <summary>
        /// 根据主键进行查询对象
        /// </summary>
        /// <param name="objectType"></param>
        /// <param name="primaryKey"></param>
        /// <returns></returns>
        object Get(Type objectType, object primaryKey);
        /// <summary>
        /// 根据SQL查询对象
        /// </summary>
        /// <param name="objectType"></param>
        /// <param name="sql"></param>
        /// <returns></returns>
        object[] Query(Type objectType, string sql);
        /// <summary>
        /// 将对象插入到数据源中
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        int Insert(object obj);
        /// <summary>
        /// 将对象更新到数据源中
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        int Update(object obj);
        /// <summary>
        /// 将对象从数据源中删除
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        int Delete(object obj);
    }
}
