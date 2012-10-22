using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Core.Client
{
    /// <summary>
    /// 数据入口客户端接口
    /// </summary>
    public interface IDataPortalClient : Server.IDataPortalServer
    {
        /// <summary>
        /// 服务端是否在远程
        /// </summary>
        bool IsServerRemote { get; }
    }
}
