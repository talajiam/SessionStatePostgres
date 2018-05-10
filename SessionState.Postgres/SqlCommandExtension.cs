using System;
using System.Collections.Generic;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace SessionState.Postgres
{
    internal static class NpgsqlCommandExtension
    {
        public static NpgsqlParameter GetOutPutParameterValue(this NpgsqlCommand cmd, SqlParameterName parameterName)
        {
            return cmd.Parameters[string.Format("@{0}", (object)parameterName)];
        }
    }
}
