using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Core.Threading
{
    /// <summary>
    /// 基于数据库的锁对象
    /// </summary>
    public class DbMonitor
    {
        public bool Enter(string key)
        {
            return true;
        }

        public bool TryEnter(string key)
        {
            return false;
        }

        public void Exit(string key)
        { }
    }

    //public class 
}
