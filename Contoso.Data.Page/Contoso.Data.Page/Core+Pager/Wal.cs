﻿using Contoso.Sys;
using Pgno = System.UInt32;
namespace Contoso.Core
{
    public class Wal
    {
#if SQLITE_OMIT_WAL
        internal static SQLITE sqlite3WalOpen(VirtualFileSystem x, VirtualFile y, string z) { return 0; }
        internal static void sqlite3WalLimit(Wal x, long y) { }
        internal static SQLITE sqlite3WalClose(Wal w, int x, int y, byte z) { return 0; }
        internal static SQLITE sqlite3WalBeginReadTransaction(Wal y, int z) { return 0; }
        internal static void sqlite3WalEndReadTransaction(Wal z) { }
        internal static SQLITE sqlite3WalRead(Wal v, Pgno w, ref int x, int y, byte[] z) { return 0; }
        internal static Pgno sqlite3WalDbsize(Wal y) { return 0; }
        internal static SQLITE sqlite3WalBeginWriteTransaction(Wal y) { return 0; }
        internal static SQLITE sqlite3WalEndWriteTransaction(Wal x) { return 0; }
        internal static SQLITE sqlite3WalUndo(Wal x, int y, object z) { return 0; }
        internal static void sqlite3WalSavepoint(Wal y, object z) { }
        internal static SQLITE sqlite3WalSavepointUndo(Wal y, object z) { return 0; }
        internal static SQLITE sqlite3WalFrames(Wal u, int v, PgHdr w, Pgno x, int y, int z) { return 0; }
        internal static SQLITE sqlite3WalCheckpoint(Wal r, int s, int t, byte[] u, int v, int w, byte[] x, ref int y, ref int z) { y = 0; z = 0; return 0; }
        internal static SQLITE sqlite3WalCallback(Wal z) { return 0; }
        internal static bool sqlite3WalExclusiveMode(Wal y, int z) { return false; }
        internal static bool sqlite3WalHeapMemory(Wal z) { return false; }
#else
const int WAL_SAVEPOINT_NDATA = 4;
typedef struct Wal Wal;
int sqlite3WalOpen(VirtualFileSystem*, VirtualFile*, string , int, i64, Wal*);
int sqlite3WalClose(Wal *pWal, int sync_flags, int, u8 );
void sqlite3WalLimit(Wal*, i64);
int sqlite3WalBeginReadTransaction(Wal *pWal, int );
void sqlite3WalEndReadTransaction(Wal *pWal);
int sqlite3WalRead(Wal *pWal, Pgno pgno, int *pInWal, int nOut, u8 *pOut);
Pgno sqlite3WalDbsize(Wal *pWal);
int sqlite3WalBeginWriteTransaction(Wal *pWal);
int sqlite3WalEndWriteTransaction(Wal *pWal);
int sqlite3WalUndo(Wal *pWal, int (*xUndo)(void *, Pgno), object  *pUndoCtx);
void sqlite3WalSavepoint(Wal *pWal, u32 *aWalData);
int sqlite3WalSavepointUndo(Wal *pWal, u32 *aWalData);
int sqlite3WalFrames(Wal *pWal, int, PgHdr *, Pgno, int, int);
int sqlite3WalCheckpoint(
  Wal *pWal,                      /* Write-ahead log connection */
  int eMode,                      /* One of PASSIVE, FULL and RESTART */
  int (*xBusy)(void),            /* Function to call when busy */
  void *pBusyArg,                 /* Context argument for xBusyHandler */
  int sync_flags,                 /* Flags to sync db file with (or 0) */
  int nBuf,                       /* Size of buffer nBuf */
  u8 *zBuf,                       /* Temporary buffer to use */
  int *pnLog,                     /* OUT: Number of frames in WAL */
  int *pnCkpt                     /* OUT: Number of backfilled frames in WAL */
);
int sqlite3WalCallback(Wal *pWal);
int sqlite3WalExclusiveMode(Wal *pWal, int op);
int sqlite3WalHeapMemory(Wal *pWal);
#endif
    }
}