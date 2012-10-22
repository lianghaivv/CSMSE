using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace Core.Data
{
    #region "Attribute"

    /// <summary>
    /// 表标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class TableAttribute : Attribute
    {
        /// <summary>
        /// 全选查询SQL格式:"SELECT * FROM {0}"
        /// </summary>
        public const string SQL_QUERY_ALL = "SELECT * FROM {0}";
        /// <summary>
        /// 根据字段查询SQL格式:"SELECT * FROM {0} WHERE {1}='{2}'"
        /// </summary>
        public const string SQL_QUERY_BY_FIELD = "SELECT * FROM {0} WHERE {1}='{2}'";

        public TableAttribute(string name)
        {
            Name = name;
            IsAutoBind = true;
            Alias = string.Empty;
            QueryAllSqlFormat = SQL_QUERY_ALL;
            QueryByFieldSqlFormat = SQL_QUERY_BY_FIELD;
        }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// 是否自动绑定属性(属性自动识别为表中的列,属性名与表列名一致)
        /// </summary>
        public bool IsAutoBind { get; set; }
        /// <summary>
        /// 别名
        /// </summary>
        public string Alias { get; set; }
        /// <summary>
        /// 全选查询SQL格式
        /// </summary>
        public string QueryAllSqlFormat { get; set; }
        /// <summary>
        /// 根据字段查询SQL格式
        /// </summary>
        public string QueryByFieldSqlFormat { get; set; }
    }

    /// <summary>
    /// 字段标记(默认属性名与字段名一致)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ColumnAttribute : Attribute
    {
        public ColumnAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// 列的名称/聚合列的前缀
        /// </summary>
        public string Name { get; private set; }
    }

    /// <summary>
    /// 字段标记(仅用于查询的字段,eg.有些字段是通过JOIN操作附加的)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ColumnQueryOnlyAttribute : ColumnAttribute
    {
        public ColumnQueryOnlyAttribute(string name)
            : base(name)
        { }
    }

    /// <summary>
    /// 字段集合标记(表示该字段是一个对象,它对应数据库中的多个列)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ColumnCollectionAttribute : ColumnAttribute
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="prefix">列的前缀</param>
        public ColumnCollectionAttribute(string prefix)
            : base(prefix)
        { IsAutoBind = true; }

        /// <summary>
        /// 是否自动绑定属性(属性自动识别为表中的列,属性名与表列名一致)
        /// </summary>
        public bool IsAutoBind { get; set; }
    }

    /// <summary>
    /// 主键标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class PrimaryKeyAttribute : ColumnAttribute
    {
        public PrimaryKeyAttribute(string name)
            : base(name)
        {
            IsAutoIncrease = false;
        }

        /// <summary>
        /// 是否是自增类型; 默认false
        /// </summary>
        public bool IsAutoIncrease { get; set; }
    }

    /// <summary>
    /// 忽略字段标记(属性不识别为表列名)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class IgnoreAttribute : Attribute
    {
    }

    #endregion

    /// <summary>
    /// 数据库参数构造器
    /// </summary>
    public class DbParameterBuilder
    {
        private Dictionary<string, DataParameterInfo> dicParam = new Dictionary<string, DataParameterInfo>();

        /// <summary>
        /// 数据参数信息
        /// </summary>
        [Serializable]
        private class DataParameterInfo
        {
            /// <summary>
            /// 参数名称
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// 参数值
            /// </summary>
            public object Value { get; set; }
            /// <summary>
            /// 参数用法
            /// </summary>
            public ParameterDirection Direction { get; set; }
            /// <summary>
            /// 参数类型
            /// </summary>
            public DbType? DbType { get; set; }
            /// <summary>
            /// 参数尺寸
            /// </summary>
            public int? Size { get; set; }
            /// <summary>
            /// 构建的数据库参数
            /// </summary>
            public IDbDataParameter Parameter { get; set; }
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        public void AddParameter(string name, object value, DbType? dbType, ParameterDirection? direction, int? size)
        {
            dicParam[GetDictionaryKey(name)] = new DataParameterInfo() { Name = name, Value = value, DbType = dbType, Direction = direction ?? ParameterDirection.Input, Size = size };
        }

        /// <summary>
        /// 获取参数值
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public object GetParameterValue(string name)
        {
            string key = GetDictionaryKey(name);
            object value = null;
            if (dicParam.ContainsKey(key))
            {
                if (dicParam[key].Parameter != null)
                {
                    value = dicParam[key].Parameter.Value;
                }
            }
            return value;
        }

        /// <summary>
        /// 获取参数名称;名称去掉前置的@:?符号
        /// </summary>
        private string GetDictionaryKey(string name)
        {
            if (name != null && name.StartsWith("@") || name.StartsWith(":") || name.StartsWith("?"))
            {
                return name.Substring(1);
            }
            return name;
        }

        #region IDbParameterBuilder Members

        public void BuildParam(IDbCommand cmd)
        {
            if (cmd == null)
            { return; }

            foreach (DataParameterInfo item in dicParam.Values)
            {
                IDbDataParameter param = cmd.CreateParameter();
                param.ParameterName = item.Name;
                param.Direction = item.Direction;
                if (item.DbType != null)
                { param.DbType = item.DbType.Value; }
                param.Value = item.Value ?? DBNull.Value;
                //扩展对象
                if (item.Value is IDbExtendSerialize)
                {
                    IDbExtendSerialize extend = (IDbExtendSerialize)item.Value;
                    param.Value = extend.Serialize();
                    param.DbType = extend.GetDbType();
                }
                //字符串
                if (item.Value is string)
                {
                    string value = item.Value as string;
                    param.Size = value.Length + 1;
                    if (param.Size < 4000)
                    { param.Size = 4000; }
                }
                if (item.Size != null)
                {
                    param.Size = item.Size.Value;
                }

                item.Parameter = param; //将这个数据库参数记下来;以便作为返回值用
                cmd.Parameters.Add(param);
            }
        }

        #endregion
    }

    /// <summary>
    /// 扩展数据类型接口: 一个对象，对应数据库中的一个字段;以序列化的形式保存于数据库中
    /// </summary>
    public interface IDbExtendSerialize
    {
        /// <summary>
        /// 获取当前存储到数据库的数据类型
        /// </summary>
        /// <returns></returns>
        DbType GetDbType();
        /// <summary>
        /// 将当前对象序列化成字符串
        /// </summary>
        /// <returns></returns>
        string Serialize();
        /// <summary>
        /// 根椐序列化后的字符串转换成对象
        /// </summary>
        /// <param name="text"></param>
        object Deserialize(string text);
    }
   
    /// <summary>
    /// 数据库表的列ORM信息
    /// </summary>
    internal class ColumnInfo
    {
        public ColumnInfo(MemberInfo mi) : this(mi, string.Empty) { }
        public ColumnInfo(MemberInfo mi, string prefix)
        {
            if (mi.MemberType == MemberTypes.Property)
            {
                PropertyInfo pi = (PropertyInfo)mi;
                ColumnAttribute ca = (ColumnAttribute)Attribute.GetCustomAttribute(pi, typeof(ColumnAttribute), false);
                ColumnName = (prefix ?? string.Empty) + ((ca == null) ? pi.Name : ca.Name);
                ColumnType = pi.PropertyType;
                ColumnDbType = DbTypeMapper.GetColumnDbType(pi.PropertyType);
                Getter = DynamicMethodHandlerFactory.CreatePropertyGetter(pi);
                Setter = DynamicMethodHandlerFactory.CreatePropertySetter(pi);
                IsDbExtendSerialize = typeof(IDbExtendSerialize).IsAssignableFrom(pi.PropertyType);
                Ctor = !(IsDbExtendSerialize || ca is ColumnCollectionAttribute) ? null : DynamicMethodHandlerFactory.CreateConstructor(pi.PropertyType.GetConstructor(Type.EmptyTypes));
                Index = -1;
                Member = mi;
            }
            else if (mi.MemberType == MemberTypes.Field)
            {
                FieldInfo fi = (FieldInfo)mi;
                ColumnAttribute ca = (ColumnAttribute)Attribute.GetCustomAttribute(fi, typeof(ColumnAttribute), false);
                ColumnName = (prefix ?? string.Empty) + ((ca == null) ? fi.Name : ca.Name);
                ColumnType = fi.FieldType;
                ColumnDbType = DbTypeMapper.GetColumnDbType(fi.FieldType);
                Getter = DynamicMethodHandlerFactory.CreateFieldGetter(fi);
                Setter = DynamicMethodHandlerFactory.CreateFieldSetter(fi);
                IsDbExtendSerialize = typeof(IDbExtendSerialize).IsAssignableFrom(fi.FieldType);
                Ctor = !(IsDbExtendSerialize || ca is ColumnCollectionAttribute) ? null : DynamicMethodHandlerFactory.CreateConstructor(fi.FieldType.GetConstructor(Type.EmptyTypes));
                Index = -1;
                Member = mi;
            }
            else
            { throw new InvalidOperationException("mi MUST be Property or Field"); }
        }

        /// <summary>
        /// 数据库中的列名
        /// </summary>
        public string ColumnName { get; private set; }

        /// <summary>
        /// 数据库中的字段索引;-1表示索引没有初始化或者是聚合类
        /// </summary>
        public int Index { get; internal set; }

        /// <summary>
        /// 列的数据类型(.NET数据类型)
        /// </summary>
        public Type ColumnType { get; private set; }

        /// <summary>
        /// 列的数据类型(Db数据类型)
        /// </summary>
        public DbType ColumnDbType { get; private set; }

        /// <summary>
        /// 实体属性的getter方法
        /// </summary>
        public DynamicMemberGetDelegate Getter { get; private set; }

        /// <summary>
        /// 实体属性的setter方法
        /// </summary>
        public DynamicMemberSetDelegate Setter { get; private set; }

        /// <summary>
        /// 是否是实现了扩展数据类型IDbExtendSerialize接口;即该对象对应数据库中的一列
        /// </summary>
        public bool IsDbExtendSerialize { get; private set; }

        ///// <summary>
        ///// 是否是聚集扩展数据类型;即该对象对应数据库中多个列
        ///// </summary>
        //public bool IsDbExtendCollection { get; private set; }

        /// <summary>
        /// 扩展以象的构造函数(如果不为扩展对象;则为null)
        /// </summary>
        public DynamicCtorDelegate Ctor { get; private set; }
        
        /// <summary>
        /// 列对应的成员对象
        /// </summary>
        public MemberInfo Member { get; private set; }
    }

    /// <summary>
    /// 数据库表的列的集合
    /// </summary>
    internal class ColumnCollectionInfo : ColumnInfo, IEnumerable, IList<ColumnInfo>
    {
        private List<ColumnInfo> Value = new List<ColumnInfo>();

        public ColumnCollectionInfo(MemberInfo mi, string prefix, List<ColumnInfo> collection)
            : base(mi)
        {            
            foreach (ColumnInfo item in collection)
            {
                Value.Add(new ColumnInfo(item.Member, prefix));
            }
        }

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return Value.GetEnumerator();
        }

        #endregion

        #region IList<ColumnInfo> Members

        public int IndexOf(ColumnInfo item)
        {
            return Value.IndexOf(item);
        }

        public void Insert(int index, ColumnInfo item)
        {
            Value.Insert(index, item);
        }

        ColumnInfo IList<ColumnInfo>.this[int index]
        {
            get { return Value[index]; }
            set { Value[index] = value; }
        }

        #endregion

        #region ICollection<ColumnInfo> Members

        public void Add(ColumnInfo item)
        {
            Value.Add(item);
        }

        public bool Contains(ColumnInfo item)
        {
            return Value.Contains(item);
        }

        public void CopyTo(ColumnInfo[] array, int arrayIndex)
        {
            Value.CopyTo(array, arrayIndex); ;
        }

        public bool Remove(ColumnInfo item)
        {
            return Value.Remove(item);
        }

        #endregion

        #region IEnumerable<ColumnInfo> Members

        IEnumerator<ColumnInfo> IEnumerable<ColumnInfo>.GetEnumerator()
        {
            return Value.GetEnumerator();
        }

        #endregion

        #region IList<ColumnInfo> Members


        public void RemoveAt(int index)
        {
            Value.RemoveAt(index);
        }

        #endregion

        #region ICollection<ColumnInfo> Members


        public void Clear()
        {
            Value.Clear();
        }

        public int Count
        {
            get { return Value.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        #endregion
    }

    /// <summary>
    /// 数据库表的主键信息
    /// </summary>
    internal class PrimaryKeyInfo : ColumnInfo
    {
        public PrimaryKeyInfo(MemberInfo mi, bool isAutoIncrease)
            : base(mi)
        {
            IsAutoIncrease = isAutoIncrease;
        }

        /// <summary>
        /// 是否是自增类型; 默认false
        /// </summary>
        public bool IsAutoIncrease { get; private set; }
    }

    /// <summary>
    /// 数据表信息
    /// </summary>
    internal class TableInfo
    {
        public TableInfo()
        {
            Name = string.Empty;
            IsAutoBind = true;
            HasBindIndex = false;
        }

        /// <summary>
        /// 表名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否自动绑定属性(属性自动识别为表中的列,属性名与表列名一致)
        /// </summary>
        public bool IsAutoBind { get; set; }

        /// <summary>
        /// 主键
        /// </summary>
        public PrimaryKeyInfo PrimaryKey { get; set; }

        /// <summary>
        /// 数据库列(不含主键)
        /// </summary>
        public List<ColumnInfo> Column { get; set; }

        /// <summary>
        /// 查询操作中,通过Join产生的列;仅用于查询;不用于增删改
        /// </summary>
        public List<ColumnInfo> QueryOnlyColumn { get; set; }

        /// <summary>
        /// 实体对象的默认构造方法
        /// </summary>
        public DynamicCtorDelegate Ctor { get; set; }

        /// <summary>
        /// 是否已经绑定索引
        /// </summary>
        public bool HasBindIndex { get; set; }

        /// <summary>
        /// select sql by primary key
        /// </summary>
        public string SelectSql { get; set; }
        /// <summary>
        /// insert sql
        /// </summary>
        public string InsertSql { get; set; }
        /// <summary>
        /// update sql
        /// </summary>
        public string UpdateSql { get; set; }
        /// <summary>
        /// delete sql
        /// </summary>
        public string DeleteSql { get; set; }
        /// <summary>
        /// 别名
        /// </summary>
        public string Alias { get; set; }
        /// <summary>
        /// 全选查询SQL格式
        /// </summary>
        public string QueryAllSqlFormat { get; set; }
        /// <summary>
        /// 根据字段查询SQL格式
        /// </summary>
        public string QueryByFieldSqlFormat { get; set; }
    }

    /// <summary>
    /// Delegate for a dynamic constructor method.
    /// </summary>
    public delegate object DynamicCtorDelegate();
    /// <summary>
    /// Delegate for getting a value.
    /// </summary>
    /// <param name="target">Target object.</param>
    /// <returns></returns>
    public delegate object DynamicMemberGetDelegate(object target);
    /// <summary>
    /// Delegate for setting a value.
    /// </summary>
    /// <param name="target">Target object.</param>
    /// <param name="arg">Argument value.</param>
    public delegate void DynamicMemberSetDelegate(object target, object arg);

    /// <summary>
    /// Delegate Factory for dynamic  method
    /// </summary>
    public static class DynamicMethodHandlerFactory
    {
        public static DynamicCtorDelegate CreateConstructor(ConstructorInfo constructor)
        {
            if (constructor == null)
                throw new ArgumentNullException("constructor");
            if (constructor.GetParameters().Length > 0)
                throw new NotSupportedException("constructors with parameters not supported");

            DynamicMethod dm = new DynamicMethod(
                "ctor",
                constructor.DeclaringType,
                Type.EmptyTypes,
                true);

            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Newobj, constructor);
            il.Emit(OpCodes.Ret);

            return (DynamicCtorDelegate)dm.CreateDelegate(typeof(DynamicCtorDelegate));
        }

        public static DynamicMemberGetDelegate CreatePropertyGetter(PropertyInfo property)
        {
            if (property == null)
                throw new ArgumentNullException("property");

            if (!property.CanRead) return null;

            MethodInfo getMethod = property.GetGetMethod();
            if (getMethod == null)   //maybe is private
                getMethod = property.GetGetMethod(true);

            DynamicMethod dm = new DynamicMethod("propg", typeof(object),
                new Type[] { typeof(object) },
                property.DeclaringType, true);

            ILGenerator il = dm.GetILGenerator();

            if (!getMethod.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.EmitCall(OpCodes.Callvirt, getMethod, null);
            }
            else
                il.EmitCall(OpCodes.Call, getMethod, null);

            if (property.PropertyType.IsValueType)
                il.Emit(OpCodes.Box, property.PropertyType);

            il.Emit(OpCodes.Ret);

            return (DynamicMemberGetDelegate)dm.CreateDelegate(typeof(DynamicMemberGetDelegate));
        }

        public static DynamicMemberSetDelegate CreatePropertySetter(PropertyInfo property)
        {
            if (property == null)
                throw new ArgumentNullException("property");

            if (!property.CanWrite) return null;

            MethodInfo setMethod = property.GetSetMethod();
            if (setMethod == null)   //maybe is private
                setMethod = property.GetSetMethod(true);

            DynamicMethod dm = new DynamicMethod("props", null,
                new Type[] { typeof(object), typeof(object) },
                property.DeclaringType, true);

            ILGenerator il = dm.GetILGenerator();

            if (!setMethod.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
            }
            il.Emit(OpCodes.Ldarg_1);

            EmitCastToReference(il, property.PropertyType);
            if (!setMethod.IsStatic && !property.DeclaringType.IsValueType)
            {
                il.EmitCall(OpCodes.Callvirt, setMethod, null);
            }
            else
                il.EmitCall(OpCodes.Call, setMethod, null);

            il.Emit(OpCodes.Ret);

            return (DynamicMemberSetDelegate)dm.CreateDelegate(typeof(DynamicMemberSetDelegate));
        }

        public static DynamicMemberGetDelegate CreateFieldGetter(FieldInfo field)
        {
            if (field == null)
                throw new ArgumentNullException("field");

            DynamicMethod dm = new DynamicMethod("fldg", typeof(object),
                new Type[] { typeof(object) },
                field.DeclaringType, true);

            ILGenerator il = dm.GetILGenerator();

            if (!field.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);

                EmitCastToReference(il, field.DeclaringType);  //to handle struct object

                il.Emit(OpCodes.Ldfld, field);
            }
            else
                il.Emit(OpCodes.Ldsfld, field);

            if (field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, field.FieldType);

            il.Emit(OpCodes.Ret);

            return (DynamicMemberGetDelegate)dm.CreateDelegate(typeof(DynamicMemberGetDelegate));
        }

        public static DynamicMemberSetDelegate CreateFieldSetter(FieldInfo field)
        {
            if (field == null)
                throw new ArgumentNullException("field");

            DynamicMethod dm = new DynamicMethod("flds", null,
                new Type[] { typeof(object), typeof(object) },
                field.DeclaringType, true);

            ILGenerator il = dm.GetILGenerator();

            if (!field.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
            }
            il.Emit(OpCodes.Ldarg_1);

            EmitCastToReference(il, field.FieldType);

            if (!field.IsStatic)
                il.Emit(OpCodes.Stfld, field);
            else
                il.Emit(OpCodes.Stsfld, field);
            il.Emit(OpCodes.Ret);

            return (DynamicMemberSetDelegate)dm.CreateDelegate(typeof(DynamicMemberSetDelegate));
        }

        private static void EmitCastToReference(ILGenerator il, Type type)
        {
            if (type.IsValueType)
                il.Emit(OpCodes.Unbox_Any, type);
            else
                il.Emit(OpCodes.Castclass, type);
        }
    }

    /// <summary>
    /// 数据库类型与.NET数据类型映秀关系
    /// </summary>
    internal static class DbTypeMapper
    {
        /// <summary>
        /// .NET数据类型与数据库类型映射关系
        /// </summary>
        private static readonly Dictionary<RuntimeTypeHandle, DbType> dicDbType;

        static DbTypeMapper()
        {
            dicDbType = new Dictionary<RuntimeTypeHandle, DbType>();
            dicDbType[typeof(byte).TypeHandle] = DbType.Byte;
            dicDbType[typeof(sbyte).TypeHandle] = DbType.SByte;
            dicDbType[typeof(short).TypeHandle] = DbType.Int16;
            dicDbType[typeof(ushort).TypeHandle] = DbType.UInt16;
            dicDbType[typeof(int).TypeHandle] = DbType.Int32;
            dicDbType[typeof(uint).TypeHandle] = DbType.UInt32;
            dicDbType[typeof(long).TypeHandle] = DbType.Int64;
            dicDbType[typeof(ulong).TypeHandle] = DbType.UInt64;
            dicDbType[typeof(float).TypeHandle] = DbType.Single;
            dicDbType[typeof(double).TypeHandle] = DbType.Double;
            dicDbType[typeof(decimal).TypeHandle] = DbType.Decimal;
            dicDbType[typeof(bool).TypeHandle] = DbType.Boolean;
            dicDbType[typeof(string).TypeHandle] = DbType.String;
            dicDbType[typeof(char).TypeHandle] = DbType.StringFixedLength;
            dicDbType[typeof(Guid).TypeHandle] = DbType.Guid;
            dicDbType[typeof(DateTime).TypeHandle] = DbType.DateTime;
            dicDbType[typeof(DateTimeOffset).TypeHandle] = DbType.DateTimeOffset;
            dicDbType[typeof(byte[]).TypeHandle] = DbType.Binary;
            dicDbType[typeof(byte?).TypeHandle] = DbType.Byte;
            dicDbType[typeof(sbyte?).TypeHandle] = DbType.SByte;
            dicDbType[typeof(short?).TypeHandle] = DbType.Int16;
            dicDbType[typeof(ushort?).TypeHandle] = DbType.UInt16;
            dicDbType[typeof(int?).TypeHandle] = DbType.Int32;
            dicDbType[typeof(uint?).TypeHandle] = DbType.UInt32;
            dicDbType[typeof(long?).TypeHandle] = DbType.Int64;
            dicDbType[typeof(ulong?).TypeHandle] = DbType.UInt64;
            dicDbType[typeof(float?).TypeHandle] = DbType.Single;
            dicDbType[typeof(double?).TypeHandle] = DbType.Double;
            dicDbType[typeof(decimal?).TypeHandle] = DbType.Decimal;
            dicDbType[typeof(bool?).TypeHandle] = DbType.Boolean;
            dicDbType[typeof(char?).TypeHandle] = DbType.StringFixedLength;
            dicDbType[typeof(Guid?).TypeHandle] = DbType.Guid;
            dicDbType[typeof(DateTime?).TypeHandle] = DbType.DateTime;
            dicDbType[typeof(DateTimeOffset?).TypeHandle] = DbType.DateTimeOffset;
        }

        /// <summary>
        /// 获取数据库类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static DbType GetColumnDbType(Type type)
        {
            DbType dbType;
            Type nullUnderlyingType = Nullable.GetUnderlyingType(type);
            if (nullUnderlyingType != null)
            {
                type = nullUnderlyingType;
            }
            if (type.IsEnum)
            {
                type = Enum.GetUnderlyingType(type);
            }
            if (dicDbType.TryGetValue(type.TypeHandle, out dbType))
            {
                return dbType;
            }
            if (typeof(IEnumerable).IsAssignableFrom(type) 
             || typeof(IDbExtendSerialize).IsAssignableFrom(type))
            {
                return DbType.Xml;
            }

            return DbType.Xml;
            //throw new NotSupportedException(string.Format("type {1} cannot be used as a parameter value", type.FullName));
        }
    }

    /// <summary>
    /// 对象与表名的对应关系
    /// </summary>
    internal static class TableMapper
    {
        #region "TableInfo Cache"

        /// <summary>
        /// 数据库表信息
        /// </summary>
        private static Dictionary<RuntimeTypeHandle, TableInfo> dicTable = new Dictionary<RuntimeTypeHandle, TableInfo>();

        /// <summary>
        /// 获取数据库表信息
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static TableInfo GetTableInfo(Type type)
        {
            if (type == null)
            { throw new ArgumentNullException("type"); }

            TableInfo table = null;
            if (!dicTable.TryGetValue(type.TypeHandle, out table))
            {
                lock (dicTable) //double check lock
                {
                    if (!dicTable.TryGetValue(type.TypeHandle, out table))
                    {
                        table = new TableInfo();
                        //table name
                        object[] attrs = type.GetCustomAttributes(typeof(TableAttribute), false);
                        if (attrs != null && attrs.Length > 0)
                        {
                            TableAttribute ta = attrs[0] as TableAttribute;
                            table.Name = ta.Name;
                            table.IsAutoBind = ta.IsAutoBind;
                            table.Alias = ta.Alias;
                            table.QueryAllSqlFormat = ta.QueryAllSqlFormat;
                            table.QueryByFieldSqlFormat = ta.QueryByFieldSqlFormat;
                        }
                        else
                        {
                            table.Name = (type.Name.EndsWith("Info", StringComparison.OrdinalIgnoreCase)) ? type.Name : (type.Name + "Info"); //default table name is "[class name]" + "Info"
                            table.IsAutoBind = true;
                            table.Alias = string.Empty;
                            table.QueryAllSqlFormat = TableAttribute.SQL_QUERY_ALL;
                            table.QueryByFieldSqlFormat = TableAttribute.SQL_QUERY_BY_FIELD;
                        }

                        //primary and columns
                        table.PrimaryKey = GetTablePrimaryKey(type);
                        table.Column = GetTableColumns(type, table.IsAutoBind);
                        table.QueryOnlyColumn = GetQueryOnlyColumns(type);

                        //remove the primary key from the columns
                        if (table.PrimaryKey != null)
                        {
                            ColumnInfo ci = table.Column.Find(obj => string.Compare(obj.ColumnName, table.PrimaryKey.ColumnName) == 0);
                            if (ci != null)
                            {
                                table.Column.Remove(ci);
                            }
                        }

                        //the entity default constructor
                        table.Ctor = DynamicMethodHandlerFactory.CreateConstructor(type.GetConstructor(Type.EmptyTypes));
                        //
                        dicTable.Add(type.TypeHandle, table);
                    }
                }
            }

            return table;
        }

        /// <summary>
        /// 获取数据库表主键
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static PrimaryKeyInfo GetTablePrimaryKey(Type type)
        {
            if (type == null)
            { throw new ArgumentNullException("type"); }

            PrimaryKeyInfo pk = null;
            List<MemberInfo> lstMember = new List<MemberInfo>();
            lstMember.AddRange(type.GetProperties());
            lstMember.AddRange(type.GetFields());
            foreach (MemberInfo mi in lstMember)
            {
                PrimaryKeyAttribute pka = (PrimaryKeyAttribute)Attribute.GetCustomAttribute(mi, typeof(PrimaryKeyAttribute), false);
                if (pka != null)
                {
                    pk = new PrimaryKeyInfo(mi, pka.IsAutoIncrease);
                    break;
                }
            }
            //if the primary key is null ,auto use UID/Id as default
            if (pk == null)
            {
                foreach (MemberInfo mi in lstMember)
                {
                    if (string.Compare(mi.Name, "UID", true) == 0)
                    {
                        pk = new PrimaryKeyInfo(mi, false);
                        break;
                    }
                    if (string.Compare(mi.Name, "ID", true) == 0)
                    {
                        pk = new PrimaryKeyInfo(mi, true);
                        break;
                    }
                }
            }

            return pk;
        }

        /// <summary>
        /// 获取数据库表字段对应关系
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static List<ColumnInfo> GetTableColumns(Type type, bool isAutoBind)
        {
            if (type == null)
            { throw new ArgumentNullException("type"); }

            List<ColumnInfo> lstColumn = new List<ColumnInfo>();
            List<MemberInfo> lstMember = new List<MemberInfo>();
            lstMember.AddRange(type.GetProperties());
            lstMember.AddRange(type.GetFields());

            //filter
            if (!isAutoBind)
            {
                lstMember = lstMember.FindAll(obj => obj.IsDefined(typeof(ColumnAttribute), false));
            }

            //map
            foreach (MemberInfo mi in lstMember)
            {
                if (mi.IsDefined(typeof(IgnoreAttribute), false) //忽略的字段
                 || mi.IsDefined(typeof(PrimaryKeyAttribute), false) //主键字段
                 || mi.IsDefined(typeof(ColumnQueryOnlyAttribute), false)) //仅用于查询的字段
                { continue; }

                //忽略非扩展数据类型的对象属性/字段
                Type t = (mi is PropertyInfo) ? ((PropertyInfo)mi).PropertyType : ((FieldInfo)mi).FieldType;
                if (t.IsClass && t != typeof(string) && t != typeof(byte[])
                 && t.GetInterface(typeof(IDbExtendSerialize).FullName, false) == null
                 && !mi.IsDefined(typeof(ColumnCollectionAttribute), false))
                {
                    continue;
                }

                //忽略只读或者只写的属性
                if ((mi is PropertyInfo) && !(((PropertyInfo)mi).CanRead && ((PropertyInfo)mi).CanWrite))
                {
                    continue;
                }

                ColumnInfo ci = null;
                ColumnCollectionAttribute cca = (ColumnCollectionAttribute)Attribute.GetCustomAttribute(mi, typeof(ColumnCollectionAttribute), false);
                if (cca != null)
                {
                    //处理聚合的字段
                    //递归调用自身;生成聚合字段
                    ci = new ColumnCollectionInfo(mi, cca.Name, GetTableColumns(t, cca.IsAutoBind));
                }
                else
                {
                    //处理非聚合的字段
                    ci = new ColumnInfo(mi);
                }

                //validate
                if (!(ci is ColumnCollectionInfo))
                {
                    ColumnInfo tmp = lstColumn.Find(obj => string.Compare(obj.ColumnName, ci.ColumnName, true) == 0 && !(obj is ColumnCollectionInfo));
                    if (tmp != null)
                    {
                        throw new DataException(string.Format("Type {0} column name repeat error:{1};", type.Name, ci.ColumnName));
                    }
                }

                //add column
                lstColumn.Add(ci);
            }

            return lstColumn;
        }

        /// <summary>
        /// 获取仅用于查询的字段
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static List<ColumnInfo> GetQueryOnlyColumns(Type type)
        {
            if (type == null)
            { throw new ArgumentNullException("type"); }

            List<ColumnInfo> lstColumn = new List<ColumnInfo>();
            List<MemberInfo> lstMember = new List<MemberInfo>();
            lstMember.AddRange(type.GetProperties());
            lstMember.AddRange(type.GetFields());

            //map
            foreach (MemberInfo mi in lstMember)
            {
                if (mi.IsDefined(typeof(ColumnQueryOnlyAttribute), false)) //仅用于查询的字段
                {
                    lstColumn.Add(new ColumnInfo(mi));
                }
            }
            return lstColumn;
        }

        #endregion
    }

    /// <summary>
    /// ORM Mapper
    /// </summary>
    public static class DbMapper
    {
        #region "ORM Core"

        /// <summary>
        /// 执行查询SQL
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cnn"></param>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <returns></returns>
        public static int Execute(this IDbConnection cnn, CommandType cmdType, string cmdText, DbParameterBuilder builder)
        {
            int result = 0;
            using (IDbCommand cmd = cnn.CreateCommand())
            {
                //if the connection is close, open it.
                bool flag = (cnn.State == ConnectionState.Closed);
                if (flag)
                { cnn.Open(); }

                cmd.CommandText = cmdText;
                cmd.CommandType = cmdType;
                cmd.Parameters.Clear();
                if (builder != null)
                {
                    builder.BuildParam(cmd);
                }
                //execute sql
                result = cmd.ExecuteNonQuery();

                //auto close the connection if it is open by this method
                if (flag)
                { cnn.Close(); }
            }
            return result;
        }

        /// <summary>
        /// 执行查询SQL
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cnn"></param>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <returns></returns>
        public static List<T> Query<T>(this IDbConnection cnn, CommandType cmdType, string cmdText, DbParameterBuilder builder)
        {
            List<T> list = new List<T>();
            using (IDbCommand cmd = cnn.CreateCommand())
            {
                //if the connection is close, open it.
                bool flag = (cnn.State == ConnectionState.Closed);
                if (flag)
                { cnn.Open(); }

                cmd.CommandText = cmdText;
                cmd.CommandType = cmdType;
                cmd.Parameters.Clear();
                if (builder != null)
                {
                    builder.BuildParam(cmd);
                }

                using (IDataReader reader = cmd.ExecuteReader())
                {
                    Type t = typeof(T);
                    if (t.IsClass && t != typeof(string) && t != typeof(byte[]))
                    {
                        //class entity
                        TableInfo table = TableMapper.GetTableInfo(typeof(T));
                        while (reader.Read())
                        {
                            //deserialize DataReader ==> Objects
                            list.Add((T)Deserializer(reader, table));
                        }
                    }
                    else
                    {
                        //struct
                        Func<IDataReader, T> deserializer = Deserializer<T>(reader);
                        while (reader.Read())
                        {
                            //deserialize DataReader ==> struct
                            list.Add(deserializer(reader));
                        }
                    }
                }

                //auto close the connection if it is open by this method
                if (flag)
                { cnn.Close(); }
            }

            return list;
        }

        /// <summary>
        /// 反序列化(返回值类型)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static Func<IDataReader, T> Deserializer<T>(IDataReader reader)
        {
            Func<IDataReader, T> deserializer = null;
            if (typeof(T) == typeof(char))
            {
                deserializer = r =>
                {
                    var val = r.GetValue(0);
                    if (val == DBNull.Value)
                    { throw new ArgumentNullException("DataReader result is DBNull.Value"); }
                    string s = val as string;
                    if (s == null || s.Length != 1)
                    { throw new ArgumentException("DataReader result is NOT single-character", "reader"); }
                    return (T)val;
                };
            }
            if (typeof(T) == typeof(char?))
            {
                deserializer = r =>
                {
                    var val = r.GetValue(0);
                    if (val == DBNull.Value)
                    { return default(T); }
                    string s = val as string;
                    if (s == null || s.Length != 1)
                    { throw new ArgumentException("DataReader result is NOT single-character", "reader"); }
                    val = s[0];
                    return (T)val;
                };
            }
            if (deserializer == null)
            {
                deserializer = r =>
                {
                    var val = r.GetValue(0);
                    if (val == DBNull.Value)
                    { val = null; }
                    return (T)val;
                };
            }

            return deserializer;
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static object Deserializer(IDataReader reader, TableInfo table)
        {
            List<ColumnInfo> lstColumn = new List<ColumnInfo>();
            lstColumn.Add(table.PrimaryKey);
            lstColumn.AddRange(table.Column);
            lstColumn.AddRange(table.QueryOnlyColumn);

            //绑定列的索引
            if (!table.HasBindIndex)
            {
                foreach (ColumnInfo ci in lstColumn)
                {
                    BindColumnIndex(reader, ci);
                }
                table.HasBindIndex = true;
                //排序
            }

            //create new object
            object entity = table.Ctor.Invoke();
            //bind column value
            foreach (ColumnInfo ci in lstColumn)
            {
                BindColumnValue(reader, ci, entity);
            }

            return entity;
        }

        /// <summary>
        /// 绑定列的索引
        /// </summary>
        /// <param name="prefix">聚合列的前缀</param>
        private static void BindColumnIndex(IDataReader reader, ColumnInfo ci)
        {
            if (ci == null) { return; }
            //处理聚合列
            if (ci is ColumnCollectionInfo)
            {
                foreach (ColumnInfo item in (ColumnCollectionInfo)ci)
                {
                    //递归绑定索引
                    BindColumnIndex(reader, item);
                }
            }
            else
            {
                //绑定列索引
                ci.Index = reader.GetOrdinal(ci.ColumnName);
            }
        }

        /// <summary>
        /// 绑定列的值
        /// </summary>
        /// <param name="prefix">聚合列的前缀</param>
        private static void BindColumnValue(IDataReader reader, ColumnInfo ci, object entity)
        {
            if (ci == null) { return; }
            //处理聚合列
            if (ci is ColumnCollectionInfo)
            {
                ColumnCollectionInfo cci = (ColumnCollectionInfo)ci;
                object obj = cci.Ctor();
                cci.Setter(entity, obj);
                foreach (ColumnInfo item in cci)
                {
                    //递归绑定聚合属性的各列值
                    BindColumnValue(reader, item, obj);
                }
            }
            else
            {
                //绑定列值
                if (ci.Index > -1 && ci.Index < reader.FieldCount)
                {
                    if (ci.IsDbExtendSerialize)
                    {
                        IDbExtendSerialize extend = ci.Ctor.Invoke() as IDbExtendSerialize;
                        object obj = extend.Deserialize(reader[ci.Index].ToString());
                        if (ci.Setter != null)
                        { ci.Setter(entity, obj); }
                    }
                    else
                    {
                        object obj = reader[ci.Index];
                        if (ci.ColumnType.IsEnum)
                        {
                            //null值，不赋值
                            if (obj != DBNull.Value)
                            {
                                ci.Setter(entity, Enum.Parse(ci.ColumnType, obj.ToString(), true));
                            }
                        }
                        else
                        {
                            //null值，不赋值
                            if (obj != DBNull.Value)
                            {
                                //处理char(36)自动转换成guid类型的情况
                                if (ci.ColumnType == typeof(string) && obj.GetType() == typeof(Guid))
                                { ci.Setter(entity, obj.ToString()); }
                                else
                                { ci.Setter(entity, obj); }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 绑定名称列表
        /// </summary>
        /// <param name="ci"></param>
        /// <param name="lstColumnName"></param>
        private static void BindColumnName(ColumnInfo ci, ref List<string> lstColumnName)
        {
            if (ci == null) { return; }
            //集合列
            if (ci is ColumnCollectionInfo)
            {
                foreach (ColumnInfo item in (ColumnCollectionInfo)ci)
                {
                    BindColumnName(item, ref lstColumnName);
                }
            }
            else
            {
                lstColumnName.Add(ci.ColumnName);
            }
        }

        /// <summary>
        /// 绑定参数
        /// </summary>
        /// <param name="ci"></param>
        /// <param name="builder"></param>
        private static void BindColumnParameter(ColumnInfo ci, object entity, ref DbParameterBuilder builder)
        {
            if (ci == null) { return; }
            //集合列
            if (ci is ColumnCollectionInfo)
            {
                ColumnCollectionInfo cci = (ColumnCollectionInfo)ci;
                object obj = cci.Getter(entity);
                foreach (ColumnInfo item in cci)
                {
                    BindColumnParameter(item, obj, ref builder);
                }
            }
            else
            {
                object value = ci.Getter(entity);
                ParameterDirection direction = ((ci is PrimaryKeyInfo) && (ci as PrimaryKeyInfo).IsAutoIncrease) ? ParameterDirection.InputOutput : ParameterDirection.Input;
                builder.AddParameter(ci.ColumnName, value, ci.ColumnDbType, direction, null);
            }
        }

        #endregion

        #region "ORM 增删改查"

        /// <summary>
        /// 获取数据库参数名称前缀
        /// </summary>
        /// <param name="cnn"></param>
        /// <returns></returns>
        public static string GetParamPrefix(this IDbConnection cnn)
        {
            string type = cnn.GetType().ToString();
            if (string.Compare(type, "System.Data.SqlClient.SqlConnection", false) == 0)
            { return "@"; }
            if (string.Compare(type, "System.Data.OracleClient.OracleConnection", false) == 0)
            { return ":"; }
            if (string.Compare(type, "MySql.Data.MySqlClient.MySqlConnection", false) == 0)
            { return "?"; }
            //default
            return "@";
        }

        /// <summary>
        /// 根据主键,获取数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cnn"></param>
        /// <param name="primaryKey"></param>
        /// <returns></returns>
        public static T Get<T>(this IDbConnection cnn, object primaryKey)
        {
            Type t = typeof(T);
            TableInfo table = TableMapper.GetTableInfo(t);

            //validate
            if (table.PrimaryKey == null)
            { throw new NotSupportedException("\"Get\" function not supporte: PrimaryKey column NOT exists!"); }

            //get sql
            if (string.IsNullOrEmpty(table.SelectSql))
            {
                //bind select sql
                if (string.IsNullOrEmpty(table.Alias))
                {
                    table.SelectSql = string.Format("{0} WHERE {1}={2}{1}", string.Format(table.QueryAllSqlFormat, table.Name), table.PrimaryKey.ColumnName, GetParamPrefix(cnn));
                }
                else
                {
                    table.SelectSql = string.Format("{0} WHERE {3}.{1}={2}{1}", string.Format(table.QueryAllSqlFormat, table.Name), table.PrimaryKey.ColumnName, GetParamPrefix(cnn), table.Alias);
                }
            }

            //build parameter
            DbParameterBuilder builder = new DbParameterBuilder();
            builder.AddParameter(table.PrimaryKey.ColumnName, primaryKey, table.PrimaryKey.ColumnDbType, ParameterDirection.Input, null);

            IList<T> list = Query<T>(cnn, CommandType.Text, table.SelectSql, builder);
            if (list != null && list.Count > 0)
            { return list[0]; }
            else
            { return default(T); }
        }

        /// <summary>
        /// 将数据更新到数据库中
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static int Update(this IDbConnection cnn, object entity)
        {
            Type t = entity.GetType();
            TableInfo table = TableMapper.GetTableInfo(t);

            //validate
            if (table.PrimaryKey == null)
            { throw new NotSupportedException("\"Update\" function not supporte: PrimaryKey column NOT exists!"); }

            //get sql
            if (string.IsNullOrEmpty(table.UpdateSql) || !table.HasBindIndex)
            {
                List<string> lstColumn = new List<string>();
                foreach (ColumnInfo item in table.Column)
                {
                    BindColumnName(item, ref lstColumn);
                }

                //bind update sql
                string prefix = GetParamPrefix(cnn);
                StringBuilder sbText = new StringBuilder();
                foreach (string item in lstColumn)
                {
                    sbText.AppendFormat("{0}={1}{0},", item, prefix);
                }
                sbText.Length -= 1;
                table.UpdateSql = string.Format("UPDATE {0} SET {1} WHERE {2}={3}{2}", table.Name, sbText.ToString(), table.PrimaryKey.ColumnName, prefix);
            }

            //build parameter
            DbParameterBuilder builder = new DbParameterBuilder();
            BindColumnParameter(table.PrimaryKey, entity, ref builder);
            foreach (ColumnInfo item in table.Column)
            {
                BindColumnParameter(item, entity, ref builder);
            }

            return Execute(cnn, CommandType.Text, table.UpdateSql, builder);
        }

        /// <summary>
        /// 将数据从数据库删除
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static int Delete(this IDbConnection cnn, object entity)
        {
            Type t = entity.GetType();
            TableInfo table = TableMapper.GetTableInfo(t);            
            
            //validate
            if (table.PrimaryKey == null)
            { throw new NotSupportedException("\"Delete\" function not supporte: PrimaryKey column NOT exists!"); }

            //get sql
            if (string.IsNullOrEmpty(table.DeleteSql))
            {
                //bind delete sql
                table.DeleteSql = string.Format("DELETE FROM {0} WHERE {1}={2}{1}", table.Name, table.PrimaryKey.ColumnName, GetParamPrefix(cnn));
            }

            //build parameter
            object value = table.PrimaryKey.Getter(entity);
            DbParameterBuilder builder = new DbParameterBuilder();
            builder.AddParameter(table.PrimaryKey.ColumnName, value, table.PrimaryKey.ColumnDbType, ParameterDirection.Input, null);

            return Execute(cnn, CommandType.Text, table.DeleteSql, builder);
        }

        /// <summary>
        /// 将数据插入到数据库中
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static int Insert(this IDbConnection cnn, object entity)
        {
            Type t = entity.GetType();
            TableInfo table = TableMapper.GetTableInfo(t);

            //get sql
            if (string.IsNullOrEmpty(table.InsertSql) || !table.HasBindIndex)
            {
                List<string> lstColumn = new List<string>();
                if (table.PrimaryKey != null && !table.PrimaryKey.IsAutoIncrease)
                {
                    BindColumnName(table.PrimaryKey, ref lstColumn);
                }
                foreach (ColumnInfo item in table.Column)
                {
                    BindColumnName(item, ref lstColumn);
                }
                //bind insert sql
                string prefix = GetParamPrefix(cnn);
                table.InsertSql = string.Format("INSERT INTO {0} ({1}) VALUES ({2})", table.Name, string.Join(",", lstColumn.ToArray()), prefix + string.Join("," + prefix, lstColumn.ToArray()));
                if (table.PrimaryKey != null && table.PrimaryKey.IsAutoIncrease)
                {
                    table.InsertSql += string.Format(";SELECT {1}{0}=@@IDENTITY; ", table.PrimaryKey.ColumnName, prefix);
                }
            }

            //build parameter
            DbParameterBuilder builder = new DbParameterBuilder();
            BindColumnParameter(table.PrimaryKey, entity, ref builder);
            foreach (ColumnInfo item in table.Column)
            {
                BindColumnParameter(item, entity, ref builder);
            }
            //excute;
            int result = Execute(cnn, CommandType.Text, table.InsertSql, builder);
            if (table.PrimaryKey != null && table.PrimaryKey.IsAutoIncrease)
            {
                //bind primary key
                table.PrimaryKey.Setter(entity, builder.GetParameterValue(table.PrimaryKey.ColumnName));
            }

            //
            return result;
        }

        #endregion
    }

    /// <summary>
    /// 扩展数据类型接口: 一个对象，对应数据库中的一个字段;以XML序列化的形式保存于数据库中
    /// </summary>
    [Serializable]
    public abstract class DbExtendXmlSerialize : IDbExtendSerialize
    {
        #region "Helper function"

        //类的属性
        private class MemberCacheInfo
        {
            public string Name { get; set; }
            public DynamicMemberGetDelegate Getter { get; set; }
            public DynamicMemberSetDelegate Setter { get; set; }
        }

        /// <summary>
        /// 缓存类型的getter/setter
        /// </summary>
        private class MemberCacheContainer
        {
            //属性cache
            private static readonly object _SyncRoot = new object();
            private static Dictionary<RuntimeTypeHandle, List<MemberCacheInfo>> dicCache = new Dictionary<RuntimeTypeHandle, List<MemberCacheInfo>>();

            public static List<MemberCacheInfo> GetMemberInfo(Type t)
            {
                List<MemberCacheInfo> list = null;
                //映射到当前对象
                if (!dicCache.TryGetValue(t.TypeHandle, out list))
                {
                    lock (_SyncRoot)
                    {
                        if (!dicCache.TryGetValue(t.TypeHandle, out list))
                        {
                            list = new List<MemberCacheInfo>();
                            PropertyInfo[] pis = t.GetProperties();
                            foreach (PropertyInfo pi in pis)
                            {
                                if (pi.CanRead && pi.CanWrite)
                                {
                                    MemberCacheInfo mci = new MemberCacheInfo();
                                    mci.Name = pi.Name;
                                    mci.Getter = DynamicMethodHandlerFactory.CreatePropertyGetter(pi);
                                    mci.Setter = DynamicMethodHandlerFactory.CreatePropertySetter(pi);
                                    list.Add(mci);
                                }
                            }
                            dicCache[t.TypeHandle] = list;
                        }
                    }
                }
                return list;
            }
        }

        #endregion

        #region IExtendObject Members

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="text"></param>
        public object Deserialize(string text)
        {
            if (string.IsNullOrEmpty(text))
            { return null; }
            if (text.StartsWith("<?xml version=\"1.0\" encoding=\"utf-16\"?>", StringComparison.OrdinalIgnoreCase))
            { text = text.Substring(39); }
            MemoryStream ms = new MemoryStream();
            byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
            ms.Write(data, 0, data.Length);
            ms.Flush();
            ms.Position = 0;

            Type t = this.GetType();
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(t);
            object entity = xs.Deserialize(ms);

            //给当有对象所有属性赋值
            List<MemberCacheInfo> list = MemberCacheContainer.GetMemberInfo(t);
            foreach (MemberCacheInfo item in list)
            {
                item.Setter(this, item.Getter(entity));
            }

            return this;
        }

        /// <summary>
        /// 序列化
        /// </summary>
        /// <returns></returns>
        public string Serialize()
        {
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(this.GetType());
            StringBuilder sbXml = new StringBuilder();
            XmlWriter xw = XmlWriter.Create(sbXml);
            xs.Serialize(xw, this);
            return sbXml.ToString();
        }

        /// <summary>
        /// 数据类型
        /// </summary>
        /// <returns></returns>
        public DbType GetDbType()
        {
            return DbType.Xml;
        }

        public override string ToString()
        {
            return Serialize();
        }

        #endregion
    }
}
