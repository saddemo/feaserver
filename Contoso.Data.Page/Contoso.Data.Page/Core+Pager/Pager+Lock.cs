﻿using System.Diagnostics;
using Contoso.Sys;
using VFSLOCK = Contoso.Sys.VirtualFile.LOCK;
namespace Contoso.Core
{
    public partial class Pager
    {
        internal void pagerUnlockIfUnused()
        {
            if (this.pPCache.sqlite3PcacheRefCount() == 0)
                pagerUnlockAndRollback();
        }

        internal void pagerUnlockAndRollback()
        {
            if (this.eState != PAGER.ERROR && this.eState != PAGER.OPEN)
            {
                Debug.Assert(assert_pager_state());
                if (this.eState >= PAGER.WRITER_LOCKED)
                {
                    MallocEx.sqlite3BeginBenignMalloc();
                    sqlite3PagerRollback();
                    MallocEx.sqlite3EndBenignMalloc();
                }
                else if (!this.exclusiveMode)
                {
                    Debug.Assert(this.eState == PAGER.READER);
                    pager_end_transaction(0);
                }
            }
            pager_unlock();
        }

        internal RC pagerUnlockDb(VFSLOCK eLock)
        {
            var rc = RC.OK;
            Debug.Assert(!this.exclusiveMode || this.eLock == eLock);
            Debug.Assert(eLock == VFSLOCK.NO || eLock == VFSLOCK.SHARED);
            Debug.Assert(eLock != VFSLOCK.NO || !this.pagerUseWal());
            if (this.fd.isOpen)
            {
                Debug.Assert(this.eLock >= eLock);
                rc = this.fd.xUnlock(eLock);
                if (this.eLock != VFSLOCK.UNKNOWN)
                    this.eLock = eLock;
                SysEx.IOTRACE("UNLOCK {0:x} {1}", this.GetHashCode(), eLock);
            }
            return rc;
        }

        internal RC pagerLockDb(VFSLOCK eLock)
        {
            var rc = RC.OK;
            Debug.Assert(eLock == VFSLOCK.SHARED || eLock == VFSLOCK.RESERVED || eLock == VFSLOCK.EXCLUSIVE);
            if (this.eLock < eLock || this.eLock == VFSLOCK.UNKNOWN)
            {
                rc = this.fd.xLock(eLock);
                if (rc == RC.OK && (this.eLock != VFSLOCK.UNKNOWN || eLock == VFSLOCK.EXCLUSIVE))
                {
                    this.eLock = eLock;
                    SysEx.IOTRACE("LOCK {0:x} {1}", this.GetHashCode(), eLock);
                }
            }
            return rc;
        }

        internal RC sqlite3PagerExclusiveLock()
        {
            var rc = RC.OK;
            Debug.Assert(this.eState == PAGER.WRITER_CACHEMOD || this.eState == PAGER.WRITER_DBMOD || this.eState == PAGER.WRITER_LOCKED);
            Debug.Assert(assert_pager_state());
            if (!pagerUseWal())
                rc = pager_wait_on_lock(VFSLOCK.EXCLUSIVE);
            return rc;
        }
    }
}
