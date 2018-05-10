using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace SessionState.Postgres
{
    internal static class SqlParameterCollectionExtension
    {
        public static NpgsqlParameterCollection AddSessionIdParameter(this NpgsqlParameterCollection pc, string id)
        {
            NpgsqlParameter sqlParameter = new NpgsqlParameter(string.Format("@{0}", (object)SqlParameterName.SessionId), NpgsqlDbType.Varchar, SqlSessionStateRepositoryUtil.IdLength);
            sqlParameter.Value = (object)id;
            pc.Add(sqlParameter);
            return pc;
        }

        public static NpgsqlParameterCollection AddLockedParameter(this NpgsqlParameterCollection pc)
        {
            NpgsqlParameter sqlParameter = new NpgsqlParameter(string.Format("@{0}", (object)SqlParameterName.Locked), NpgsqlDbType.Bit);
            sqlParameter.Direction = ParameterDirection.Output;
            sqlParameter.Value = Convert.DBNull;
            pc.Add(sqlParameter);
            return pc;
        }

        public static NpgsqlParameterCollection AddLockAgeParameter(this NpgsqlParameterCollection pc)
        {
            NpgsqlParameter sqlParameter = new NpgsqlParameter(string.Format("@{0}", (object)SqlParameterName.LockAge), NpgsqlDbType.Integer);
            sqlParameter.Direction = ParameterDirection.Output;
            sqlParameter.Value = Convert.DBNull;
            pc.Add(sqlParameter);
            return pc;
        }

        public static NpgsqlParameterCollection AddLockCookieParameter(this NpgsqlParameterCollection pc, object lockId = null)
        {
            NpgsqlParameter sqlParameter = new NpgsqlParameter(string.Format("@{0}", (object)SqlParameterName.LockCookie), NpgsqlDbType.Integer);
            if (lockId == null)
            {
                sqlParameter.Direction = ParameterDirection.Output;
                sqlParameter.Value = Convert.DBNull;
            }
            else
                sqlParameter.Value = lockId;
            pc.Add(sqlParameter);
            return pc;
        }

        public static NpgsqlParameterCollection AddActionFlagsParameter(this NpgsqlParameterCollection pc)
        {
            NpgsqlParameter sqlParameter = new NpgsqlParameter(string.Format("@{0}", (object)SqlParameterName.ActionFlags), NpgsqlDbType.Integer);
            sqlParameter.Direction = ParameterDirection.Output;
            sqlParameter.Value = Convert.DBNull;
            pc.Add(sqlParameter);
            return pc;
        }

        public static NpgsqlParameterCollection AddTimeoutParameter(this NpgsqlParameterCollection pc, int timeout)
        {

            NpgsqlParameter sqlParameter = new NpgsqlParameter(string.Format("@{0}", (object)SqlParameterName.Timeout), NpgsqlDbType.Integer);
            sqlParameter.Value = (object)timeout;
            pc.Add(sqlParameter);
            AddExpiresTimeParameter(pc, timeout);
            return pc;
        }

        public static NpgsqlParameterCollection AddSessionItemLongParameter(this NpgsqlParameterCollection pc, int length, byte[] buf)
        {
            NpgsqlParameter sqlParameter = new NpgsqlParameter(string.Format("@{0}", (object)SqlParameterName.SessionItemLong), NpgsqlDbType.Bytea, length);
            sqlParameter.Value = (object)buf;
            pc.Add(sqlParameter);
            return pc;


        }


        public static NpgsqlParameterCollection AddExpiresTimeParameter(this NpgsqlParameterCollection pc, int timeout)
        {
            NpgsqlParameter sqlParameter = new NpgsqlParameter(string.Format("@{0}", (object)SqlParameterName.Expires), NpgsqlDbType.Timestamp);
            sqlParameter.Value = DateTime.UtcNow.AddMinutes(timeout);
            pc.Add(sqlParameter);
            return pc;
        }

        public static NpgsqlParameterCollection AddLockDateParameter(this NpgsqlParameterCollection pc)
        {
            NpgsqlParameter sqlParameter = new NpgsqlParameter(string.Format("@{0}", (object)SqlParameterName.LockDate), NpgsqlDbType.Timestamp);
            sqlParameter.Value = DateTime.UtcNow;
            pc.Add(sqlParameter);
            return pc;


        }

        public static NpgsqlParameterCollection AddLockDateLocalParameter(this NpgsqlParameterCollection pc)
        {
            NpgsqlParameter sqlParameter = new NpgsqlParameter(string.Format("@{0}", (object)SqlParameterName.LockDateLocal), NpgsqlDbType.Timestamp);
            sqlParameter.Value = DateTime.Now;
            pc.Add(sqlParameter);
            return pc;


        }


    }
}
