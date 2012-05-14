﻿using System;
using Pgno = System.UInt32;
using System.Diagnostics;
using Contoso.Sys;
using System.Text;
using DbPage = Contoso.Core.PgHdr;
using LOCK = Contoso.Sys.VirtualFile.LOCK;
namespace Contoso.Core
{
    public partial class Pager
    {
        // Read a 32-bit integer from the given file descriptor.  Store the integer that is read in pRes.  Return SQLITE.OK if everything worked, or an
        // error code is something goes wrong.
        // All values are stored on disk as big-endian.
        internal static SQLITE read32bits(VirtualFile fd, int offset, ref int pRes)
        {
            uint u32_pRes = 0;
            var rc = read32bits(fd, offset, ref u32_pRes);
            pRes = (int)u32_pRes;
            return rc;
        }
        internal static SQLITE read32bits(VirtualFile fd, long offset, ref uint pRes) { return read32bits(fd, (int)offset, ref pRes); }
        internal static SQLITE read32bits(VirtualFile fd, int offset, ref uint pRes)
        {
            var ac = new byte[4];
            var rc = FileEx.sqlite3OsRead(fd, ac, ac.Length, offset);
            pRes = (rc == SQLITE.OK ? ConvertEx.sqlite3Get4byte(ac) : 0);
            return rc;
        }

        // Write a 32-bit integer into the given file descriptor.  Return SQLITE.OK on success or an error code is something goes wrong.
        internal static SQLITE write32bits(VirtualFile fd, long offset, uint val)
        {
            var ac = new byte[4];
            ConvertEx.put32bits(ac, val);
            return FileEx.sqlite3OsWrite(fd, ac, 4, offset);
        }

        internal static SQLITE sqlite3PagerBegin(Pager pPager, bool exFlag, int subjInMemory)
        {
            if (pPager.errCode != 0)
                return pPager.errCode;
            Debug.Assert(pPager.eState >= PAGER.READER && pPager.eState < PAGER.ERROR);
            pPager.subjInMemory = (byte)subjInMemory;
            var rc = SQLITE.OK;
            if (Check.ALWAYS(pPager.eState == PAGER.READER))
            {
                Debug.Assert(pPager.pInJournal == null);
                if (pPager.pagerUseWal())
                {
                    // If the pager is configured to use locking_mode=exclusive, and an exclusive lock on the database is not already held, obtain it now.
                    if (pPager.exclusiveMode && Wal.sqlite3WalExclusiveMode(pPager.pWal, -1))
                    {
                        rc = pagerLockDb(pPager, LOCK.EXCLUSIVE);
                        if (rc != SQLITE.OK)
                            return rc;
                        Wal.sqlite3WalExclusiveMode(pPager.pWal, 1);
                    }
                    // Grab the write lock on the log file. If successful, upgrade to PAGER_RESERVED state. Otherwise, return an error code to the caller.
                    // The busy-handler is not invoked if another connection already holds the write-lock. If possible, the upper layer will call it.
                    rc = Wal.sqlite3WalBeginWriteTransaction(pPager.pWal);
                }
                else
                {
                    // Obtain a RESERVED lock on the database file. If the exFlag parameter is true, then immediately upgrade this to an EXCLUSIVE lock. The
                    // busy-handler callback can be used when upgrading to the EXCLUSIVE lock, but not when obtaining the RESERVED lock.
                    rc = pagerLockDb(pPager, LOCK.RESERVED);
                    if (rc == SQLITE.OK && exFlag)
                        rc = pager_wait_on_lock(pPager, LOCK.EXCLUSIVE);
                }
                if (rc == SQLITE.OK)
                {
                    // Change to WRITER_LOCKED state.
                    // WAL mode sets Pager.eState to PAGER_WRITER_LOCKED or CACHEMOD when it has an open transaction, but never to DBMOD or FINISHED.
                    // This is because in those states the code to roll back savepoint transactions may copy data from the sub-journal into the database 
                    // file as well as into the page cache. Which would be incorrect in WAL mode.
                    pPager.eState = PAGER.WRITER_LOCKED;
                    pPager.dbHintSize = pPager.dbSize;
                    pPager.dbFileSize = pPager.dbSize;
                    pPager.dbOrigSize = pPager.dbSize;
                    pPager.journalOff = 0;
                }
                Debug.Assert(rc == SQLITE.OK || pPager.eState == PAGER.READER);
                Debug.Assert(rc != SQLITE.OK || pPager.eState == PAGER.WRITER_LOCKED);
                Debug.Assert(assert_pager_state(pPager));
            }
            PAGERTRACE("TRANSACTION %d\n", PAGERID(pPager));
            return rc;
        }

        internal static SQLITE sqlite3PagerWrite(DbPage pDbPage)
        {
            var rc = SQLITE.OK;
            var pPg = pDbPage;
            var pPager = pPg.pPager;
            var nPagePerSector = (uint)(pPager.sectorSize / pPager.pageSize);
            Debug.Assert(pPager.eState >= PAGER.WRITER_LOCKED);
            Debug.Assert(pPager.eState != PAGER.ERROR);
            Debug.Assert(assert_pager_state(pPager));
            if (nPagePerSector > 1)
            {
                Pgno nPageCount = 0;     // Total number of pages in database file
                Pgno pg1;                // First page of the sector pPg is located on.
                Pgno nPage = 0;          // Number of pages starting at pg1 to journal
                bool needSync = false;   // True if any page has PGHDR_NEED_SYNC

                // Set the doNotSyncSpill flag to 1. This is because we cannot allow a journal header to be written between the pages journaled by
                // this function.
                Debug.Assert(
#if SQLITE_OMIT_MEMORYDB
0==MEMDB
#else
0 == pPager.memDb
#endif
);
                Debug.Assert(pPager.doNotSyncSpill == 0);
                pPager.doNotSyncSpill++;
                // This trick assumes that both the page-size and sector-size are an integer power of 2. It sets variable pg1 to the identifier
                // of the first page of the sector pPg is located on.
                pg1 = (Pgno)((pPg.pgno - 1) & ~(nPagePerSector - 1)) + 1;
                nPageCount = pPager.dbSize;
                if (pPg.pgno > nPageCount)
                    nPage = (pPg.pgno - pg1) + 1;
                else if ((pg1 + nPagePerSector - 1) > nPageCount)
                    nPage = nPageCount + 1 - pg1;
                else
                    nPage = nPagePerSector;
                Debug.Assert(nPage > 0);
                Debug.Assert(pg1 <= pPg.pgno);
                Debug.Assert((pg1 + nPage) > pPg.pgno);
                for (var ii = 0; ii < nPage && rc == SQLITE.OK; ii++)
                {
                    var pg = (Pgno)(pg1 + ii);
                    var pPage = new PgHdr();
                    if (pg == pPg.pgno || pPager.pInJournal.sqlite3BitvecTest(pg) == 0)
                    {
                        if (pg != ((VirtualFile.PENDING_BYTE / (pPager.pageSize)) + 1))
                        {
                            rc = sqlite3PagerGet(pPager, pg, ref pPage);
                            if (rc == SQLITE.OK)
                            {
                                rc = pager_write(pPage);
                                if ((pPage.flags & PgHdr.PGHDR.NEED_SYNC) != 0)
                                    needSync = true;
                                sqlite3PagerUnref(pPage);
                            }
                        }
                    }
                    else if ((pPage = pager_lookup(pPager, pg)) != null)
                    {
                        if ((pPage.flags & PgHdr.PGHDR.NEED_SYNC) != 0)
                            needSync = true;
                        sqlite3PagerUnref(pPage);
                    }
                }
                // If the PGHDR_NEED_SYNC flag is set for any of the nPage pages starting at pg1, then it needs to be set for all of them. Because
                // writing to any of these nPage pages may damage the others, the journal file must contain sync()ed copies of all of them
                // before any of them can be written out to the database file.
                if (rc == SQLITE.OK && needSync)
                {
                    Debug.Assert(
#if SQLITE_OMIT_MEMORYDB
0==MEMDB
#else
0 == pPager.memDb
#endif
);
                    for (var ii = 0; ii < nPage; ii++)
                    {
                        var pPage = pager_lookup(pPager, (Pgno)(pg1 + ii));
                        if (pPage != null)
                        {
                            pPage.flags |= PgHdr.PGHDR.NEED_SYNC;
                            sqlite3PagerUnref(pPage);
                        }
                    }
                }
                Debug.Assert(pPager.doNotSyncSpill == 1);
                pPager.doNotSyncSpill--;
            }
            else
                rc = pager_write(pDbPage);
            return rc;
        }

        internal static void sqlite3PagerDontWrite(PgHdr pPg)
        {
            var pPager = pPg.pPager;
            if ((pPg.flags & PgHdr.PGHDR.DIRTY) != 0 && pPager.nSavepoint == 0)
            {
                PAGERTRACE("DONT_WRITE page %d of %d\n", pPg.pgno, PAGERID(pPager));
                SysEx.IOTRACE("CLEAN %p %d\n", pPager, pPg.pgno);
                pPg.flags |= PgHdr.PGHDR.DONT_WRITE;
                pager_set_pagehash(pPg);
            }
        }

        internal static void sqlite3PagerTruncateImage(Pager pPager, uint nPage)
        {
            Debug.Assert(pPager.dbSize >= nPage);
            Debug.Assert(pPager.eState >= PAGER.WRITER_CACHEMOD);
            pPager.dbSize = nPage;
            assertTruncateConstraint(pPager);
        }

        internal static SQLITE pagerStress(object p, PgHdr pPg)
        {
            var pPager = (Pager)p;
            var rc = SQLITE.OK;
            Debug.Assert(pPg.pPager == pPager);
            Debug.Assert((pPg.flags & PgHdr.PGHDR.DIRTY) != 0);
            // The doNotSyncSpill flag is set during times when doing a sync of journal (and adding a new header) is not allowed.  This occurs
            // during calls to sqlite3PagerWrite() while trying to journal multiple pages belonging to the same sector.
            // The doNotSpill flag inhibits all cache spilling regardless of whether or not a sync is required.  This is set during a rollback.
            // Spilling is also prohibited when in an error state since that could lead to database corruption.   In the current implementaton it 
            // is impossible for sqlite3PCacheFetch() to be called with createFlag==1 while in the error state, hence it is impossible for this routine to
            // be called in the error state.  Nevertheless, we include a NEVER() test for the error state as a safeguard against future changes.
            if (Check.NEVER(pPager.errCode != 0))
                return SQLITE.OK;
            if (pPager.doNotSpill != 0)
                return SQLITE.OK;
            if (pPager.doNotSyncSpill != 0 && (pPg.flags & PgHdr.PGHDR.NEED_SYNC) != 0)
                return SQLITE.OK;
            pPg.pDirty = null;
            if (pPager.pagerUseWal())
            {
                // Write a single frame for this page to the log.
                if (subjRequiresPage(pPg))
                    rc = subjournalPage(pPg);
                if (rc == SQLITE.OK)
                    rc = pPager.pagerWalFrames(pPg, 0, 0, 0);
            }
            else
            {
                // Sync the journal file if required. 
                if ((pPg.flags & PgHdr.PGHDR.NEED_SYNC) != 0 || pPager.eState == PAGER.WRITER_CACHEMOD)
                    rc = syncJournal(pPager, 1);
                // If the page number of this page is larger than the current size of the database image, it may need to be written to the sub-journal.
                // This is because the call to pager_write_pagelist() below will not actually write data to the file in this case.
                // Consider the following sequence of events:
                //   BEGIN;
                //     <journal page X>
                //     <modify page X>
                //     SAVEPOINT sp;
                //       <shrink database file to Y pages>
                //       pagerStress(page X)
                //     ROLLBACK TO sp;
                // If (X>Y), then when pagerStress is called page X will not be written out to the database file, but will be dropped from the cache. Then,
                // following the "ROLLBACK TO sp" statement, reading page X will read data from the database file. This will be the copy of page X as it
                // was when the transaction started, not as it was when "SAVEPOINT sp" was executed.
                // The solution is to write the current data for page X into the sub-journal file now (if it is not already there), so that it will
                // be restored to its current value when the "ROLLBACK TO sp" is executed.
                if (Check.NEVER(rc == SQLITE.OK && pPg.pgno > pPager.dbSize && subjRequiresPage(pPg)))
                    rc = subjournalPage(pPg);
                // Write the contents of the page out to the database file.
                if (rc == SQLITE.OK)
                {
                    Debug.Assert((pPg.flags & PgHdr.PGHDR.NEED_SYNC) == 0);
                    rc = pager_write_pagelist(pPager, pPg);
                }
            }
            // Mark the page as clean.
            if (rc == SQLITE.OK)
            {
                PAGERTRACE("STRESS %d page %d\n", PAGERID(pPager), pPg.pgno);
                PCache.sqlite3PcacheMakeClean(pPg);
            }
            return pager_error(pPager, rc);
        }
    }
}