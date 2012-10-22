using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Core.Data
{
    /// <summary>
    /// 数据库实体基类
    /// </summary>
    [DataContract]
    [Serializable]
    public abstract class DataModel : ICloneable, IComparable
    {
        public DataModel()
        {
            //UID = Guid.NewGuid().ToString();
            //CreateTime = DateTime.Now;
            //UpdateTime = DateTime.Now;
            //Ver = 0;
        }

        private string m_UID = Guid.NewGuid().ToString();
        /// <summary>
        /// 唯一标识
        /// </summary>
        [DataMember(Name = "m_UID", Order = 0)]
        [PrimaryKey("UID")]
        public string UID
        {
            get { return m_UID; }
            set { m_UID = value; }
        }

        private DateTime m_CreateTime = DateTime.Now;
        /// <summary>
        /// 创建时间
        /// </summary>
        [DataMember(Name = "m_CreateTime", Order = 1)]
        [Column("CreateTime")]
        public DateTime CreateTime
        {
            get { return m_CreateTime; }
            set { m_CreateTime = value; }
        }

        private DateTime m_UpdateTime = DateTime.Now;
        /// <summary>
        /// 最后一次更新时间
        /// </summary>
        [DataMember(Name = "m_UpdateTime", Order = 2)]
        [Column("UpdateTime")]
        public DateTime UpdateTime
        {
            get { return m_UpdateTime; }
            set { m_UpdateTime = value; }
        }

        ///// <summary>
        ///// 唯一标识
        ///// </summary>
        //[PrimaryKey("UID")]
        //public string UID { get; set; }

        ///// <summary>
        ///// 创建时间
        ///// </summary>
        //[Column("CreateTime")]
        //public DateTime CreateTime { get; set; }

        ///// <summary>
        ///// 最后一次更新时间
        ///// </summary>
        //[Column("UpdateTime")]
        //public DateTime UpdateTime { get; set; }

        ///// <summary>
        ///// 版本号
        ///// </summary>
        //[Column("Ver")]
        //public int Ver { get; set; }

        #region "重写基本的方法与相等运算符"

        /// <summary>
        /// 重写ToString(); 等于对象的Uid
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return base.ToString() + ":" + UID.ToString();
        }

        /// <summary>
        /// 重写GetHashCode();
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// 重写验证是否相等;对象的Uid相等;即认为两者是同一个对象
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            DataModel tmp = obj as DataModel;
            return (null == tmp) ? false : UID.Equals(tmp.UID);
        }

        /// <summary>
        /// 相等比较运算符;
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool operator ==(DataModel x, DataModel y)
        {
            return object.Equals(x, y);
        }

        /// <summary>
        /// 相等比较运算符;
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool operator !=(DataModel x, DataModel y)
        {
            return !object.Equals(x, y);
        }

        #endregion

        #region ICloneable Members

        public object Clone()
        {
            //return this.MemberwiseClone();
            IFormatter formatter = new BinaryFormatter();
            using (Stream stream = new MemoryStream())
            {
                formatter.Serialize(stream, this);
                stream.Seek(0, SeekOrigin.Begin);
                return formatter.Deserialize(stream);
            }
        }

        #endregion

        #region IComparable Members

        public int CompareTo(object obj)
        {
            DataModel m = obj as DataModel;
            if (m == null)
            { return -1; }
            else
            { return this.CreateTime.CompareTo(m.CreateTime); }
        }

        #endregion
    }
}
