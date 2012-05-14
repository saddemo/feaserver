﻿using Contoso.Sys;
using System;
using System.Diagnostics;
namespace Contoso.Core
{
    public class MutexEx
    {
        static int mutexIsInit = 0;

        public enum MUTEX
        {
            FAST = 0,
            RECURSIVE = 1,
            STATIC_MASTER = 2,
            STATIC_MEM = 3,  // sqlite3_malloc()
            STATIC_MEM2 = 4,  // NOT USED
            STATIC_OPEN = 4,  // sqlite3BtreeOpen()
            STATIC_PRNG = 5,  // sqlite3_random()
            STATIC_LRU = 6,   // lru page list
            STATIC_LRU2 = 7,  // NOT USED
            STATIC_PMEM = 7, // sqlite3PageMalloc()
        }

        internal static sqlite3_mutex sqlite3_mutex_alloc(MUTEX id)
        {
            //#if !SQLITE_OMIT_AUTOINIT
            //            if (sqlite3_initialize() != 0) return null;
            //#endif
            //            return sqlite3GlobalConfig.mutex.xMutexAlloc(id);
            return null;
        }
        internal static sqlite3_mutex sqlite3MutexAlloc(MUTEX id)
        {
            //if (!sqlite3GlobalConfig.bCoreMutex)
            //    return null;
            //Debug.Assert(mutexIsInit != 0);
            //return sqlite3GlobalConfig.mutex.xMutexAlloc(id);
            return null;
        }

        internal static void sqlite3_mutex_enter(sqlite3_mutex sqlite3_mutex)
        {
        }

        internal static void sqlite3_mutex_leave(sqlite3_mutex sqlite3_mutex)
        {
        }

        internal static bool sqlite3_mutex_held(Sys.sqlite3_mutex sqlite3_mutex)
        {
            return true;
        }

        internal static bool sqlite3_mutex_notheld(Sys.sqlite3_mutex sqlite3_mutex)
        {
            return true;
        }
    }
}
