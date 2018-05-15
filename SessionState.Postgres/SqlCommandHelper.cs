using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace SessionState.Postgres
{
    internal class NpgsqlCommandHelper
    {
        private int _commandTimeout;
      
        #region property for unit tests
        internal int CommandTimeout
        {
            get { return _commandTimeout; }
        }
        #endregion

        public NpgsqlCommandHelper(int commandTimeout)
        {
            this._commandTimeout = commandTimeout;
        }

        public NpgsqlCommand CreateNewSessionTableCmd(string createSessionTableSql)
        {
            return this.CreateNpgsqlCommand(createSessionTableSql);
        }

        public NpgsqlCommand CreateGetStateItemExclusiveCmd(string getStateItemExclusiveSql, string id)
        {
            NpgsqlCommand NpgsqlCommand = this.CreateNpgsqlCommand(getStateItemExclusiveSql);
            NpgsqlCommand.Parameters.AddSessionIdParameter(id).AddLockDateParameter().AddLockDateLocalParameter();
            //.AddLockAgeParameter()
            //.AddLockedParameter().AddLockCookieParameter((object)null).AddActionFlagsParameter();
            return NpgsqlCommand;
        }

        public NpgsqlCommand CreateGetStateItemCmd(string getStateItemSql, string id)
        {
            NpgsqlCommand NpgsqlCommand = this.CreateNpgsqlCommand(getStateItemSql);
            NpgsqlCommand.Parameters.AddSessionIdParameter(id).AddLockDateParameter();
            //AddLockedParameter().AddLockAgeParameter().
            //AddLockCookieParameter((object)null).AddActionFlagsParameter();
            return NpgsqlCommand;
        }

        public NpgsqlCommand CreateDeleteExpiredSessionsCmd(string deleteExpiredSessionsSql)
        {
            NpgsqlCommand NpgsqlCommand = this.CreateNpgsqlCommand(deleteExpiredSessionsSql);
            NpgsqlCommand.Parameters.AddLockDateParameter();
            return NpgsqlCommand;
        }

        public NpgsqlCommand CreateTempInsertUninitializedItemCmd(string tempInsertUninitializedItemSql, string id, int length, byte[] buf, int timeout)
        {
            NpgsqlCommand NpgsqlCommand = this.CreateNpgsqlCommand(tempInsertUninitializedItemSql);
            NpgsqlCommand.Parameters.AddSessionIdParameter(id)
                .AddSessionItemLongParameter(length, buf)
                .AddTimeoutParameter(timeout)
                .AddLockDateParameter().AddLockDateLocalParameter();

            return NpgsqlCommand;
        }

        public NpgsqlCommand CreateReleaseItemExclusiveCmd(string releaseItemExclusiveSql, string id, object lockid)
        {
            NpgsqlCommand NpgsqlCommand = this.CreateNpgsqlCommand(releaseItemExclusiveSql);
            NpgsqlCommand.Parameters.AddSessionIdParameter(id).AddLockDateParameter();
            //AddLockCookieParameter(lockid);
            return NpgsqlCommand;
        }

        public NpgsqlCommand CreateRemoveStateItemCmd(string removeStateItemSql, string id, object lockid)
        {
            NpgsqlCommand NpgsqlCommand = this.CreateNpgsqlCommand(removeStateItemSql);
            NpgsqlCommand.Parameters.AddSessionIdParameter(id);
            //.AddLockCookieParameter(lockid);
            return NpgsqlCommand;
        }

        public NpgsqlCommand CreateResetItemTimeoutCmd(string resetItemTimeoutSql, string id)
        {
            NpgsqlCommand NpgsqlCommand = this.CreateNpgsqlCommand(resetItemTimeoutSql);
            NpgsqlCommand.Parameters.AddSessionIdParameter(id).AddLockDateParameter();
            return NpgsqlCommand;
        }

        public NpgsqlCommand CreateUpdateStateItemLongCmd(string updateStateItemLongSql, string id, byte[] buf, int length, int timeout, int lockCookie)
        {
            NpgsqlCommand NpgsqlCommand = this.CreateNpgsqlCommand(updateStateItemLongSql);
            NpgsqlCommand.Parameters.AddSessionIdParameter(id)
                .AddSessionItemLongParameter(length, buf).AddTimeoutParameter(timeout)
                .AddLockDateParameter();
            //.AddLockCookieParameter((object)lockCookie);
            return NpgsqlCommand;
        }

        public NpgsqlCommand CreateInsertStateItemLongCmd(string insertStateItemLongSql, string id, byte[] buf, int length, int timeout)
        {
            NpgsqlCommand NpgsqlCommand = this.CreateNpgsqlCommand(insertStateItemLongSql);
            NpgsqlCommand.Parameters.AddSessionIdParameter(id).AddSessionItemLongParameter(length, buf).AddTimeoutParameter(timeout)
                .AddLockDateParameter().AddLockDateLocalParameter();
            return NpgsqlCommand;
        }

        private NpgsqlCommand CreateNpgsqlCommand(string sql)
        {
            NpgsqlCommand NpgsqlCommand = new NpgsqlCommand();
            int num = 1;
            NpgsqlCommand.CommandType = (CommandType)num;
            int commandTimeout = this._commandTimeout;
            NpgsqlCommand.CommandTimeout = commandTimeout;
            string str = sql;
            NpgsqlCommand.CommandText = str;
            return NpgsqlCommand;
        }
    }
}
