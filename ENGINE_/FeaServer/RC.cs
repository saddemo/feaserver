namespace FeaServer
{
    public enum RC : int
    {
        OK = 0,
        ERROR,
        INTERNAL,
        PERM,
        ABORT,
        BUSY,
        LOCKED,
        NOMEM,
        READONLY,
        INTERRUPT,
        IOERR,
        CORRUPT,
        NOTFOUND,
        FULL,
        CANTOPEN,
        PROTOCOL,
        EMPTY,
        SCHEMA,
        TOOBIG,
        CONSTRAINT,
        MISMATCH,
        MISUSE,
        NOLFS,
        AUTH,
        FORMAT,
        RANGE,
        NOTADB,
        ROW,
        DONE,
    }
}