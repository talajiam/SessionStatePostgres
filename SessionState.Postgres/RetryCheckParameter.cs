using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace SessionState.Postgres
{
    internal class RetryCheckParameter
    {
        public NpgsqlException Exception { get; set; }

        public DateTime EndRetryTime { get; set; }

        public int RetryCount { get; set; }
    }
}
