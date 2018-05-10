using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SessionState.Postgres
{
    internal enum SqlParameterName
    {
        SessionId,
        Created,
        Expires,
        LockDate,
        LockDateLocal,
        LockCookie,
        Timeout,
        Locked,
        SessionItemLong,
        Flags,
        LockAge,
        ActionFlags,
    }
}
