using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.SessionState;
using Npgsql;

namespace SessionState.Postgres
{
    internal class PostgresSessionStateRepository : IPostgresSessionStateRepository
    {
        private static readonly string CreateSessionTableSql = string.Format("\r\n               IF NOT EXISTS (SELECT * \r\n                 FROM INFORMATION_SCHEMA.TABLES \r\n                 WHERE TABLE_NAME = '{0}')\r\n               BEGIN\r\n                CREATE TABLE {1} (\r\n                SessionId           nvarchar(88)    NOT NULL PRIMARY KEY,\r\n                Created             datetime        NOT NULL DEFAULT GETUTCDATE(),\r\n                Expires             datetime        NOT NULL,\r\n                LockDate            datetime        NOT NULL,\r\n                LockDateLocal       datetime        NOT NULL,\r\n                LockCookie          int             NOT NULL,\r\n                Timeout             int             NOT NULL,\r\n                Locked              bit             NOT NULL,\r\n                SessionItemLong     image           NULL,\r\n                Flags               int             NOT NULL DEFAULT 0,\r\n                ) \r\n                CREATE NONCLUSTERED INDEX Index_Expires ON {2} (Expires)\r\n            END", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlSessionStateRepositoryUtil.TableName);

        private static readonly string TempInsertUninitializedItemSql = string.Format(
                "INSERT INTO {0}(SessionId, SessionItemLong, Timeout, Expires, Locked, LockDate, LockDateLocal, LockCookie, Flags)VALUES" +
                "(@{1}, @{2}, @{3}, @{4},0 :: bit, @{5}, @{6},1,1)",
                (object)SqlSessionStateRepositoryUtil.TableName,
                (object)SqlParameterName.SessionId,
                (object)SqlParameterName.SessionItemLong,
                (object)SqlParameterName.Timeout,
                (object)SqlParameterName.Expires,
                (object)SqlParameterName.LockDate,
                (object)SqlParameterName.LockDateLocal);
        
        private static readonly string GetStateItemExclusiveSql = string.Format("\r\n            BEGIN TRAN\r\n                DECLARE @textptr AS varbinary(16)\r\n                DECLARE @length AS int\r\n                DECLARE @now AS datetime\r\n                DECLARE @nowLocal AS datetime\r\n            \r\n                SET @now = GETUTCDATE()\r\n                SET @nowLocal = GETDATE()\r\n            \r\n                UPDATE {0} WITH (ROWLOCK, XLOCK)\r\n                SET Expires = DATEADD(n, Timeout, @now), \r\n                    LockDate = CASE Locked\r\n                        WHEN 0 THEN @now\r\n                        ELSE LockDate\r\n                        END,\r\n                    LockDateLocal = CASE Locked\r\n                        WHEN 0 THEN @nowLocal\r\n                        ELSE LockDateLocal\r\n                        END,\r\n                    @{1} = CASE Locked\r\n                        WHEN 0 THEN 0\r\n                        ELSE DATEDIFF(second, LockDate, @now)\r\n                        END,\r\n                    @{2} = LockCookie = CASE Locked\r\n                        WHEN 0 THEN LockCookie + 1\r\n                        ELSE LockCookie\r\n                        END,\r\n                    @textptr = CASE Locked\r\n                        WHEN 0 THEN TEXTPTR(SessionItemLong)\r\n                        ELSE NULL\r\n                        END,\r\n                    @length = CASE Locked\r\n                        WHEN 0 THEN DATALENGTH(SessionItemLong)\r\n                        ELSE NULL\r\n                        END,\r\n                    @{3} = Locked,\r\n                    Locked = 1,\r\n\r\n                    /* If the Uninitialized flag (0x1) if it is set,\r\n                       remove it and return InitializeItem (0x1) in actionFlags */\r\n                    Flags = CASE\r\n                        WHEN (Flags & 1) <> 0 THEN (Flags & ~1)\r\n                        ELSE Flags\r\n                        END,\r\n                    @{4} = CASE\r\n                        WHEN (Flags & 1) <> 0 THEN 1\r\n                        ELSE 0\r\n                        END\r\n                WHERE SessionId = @{5}\r\n                IF @length IS NOT NULL BEGIN\r\n                    READTEXT {6}.SessionItemLong @textptr 0 @length\r\n                END\r\n            COMMIT TRAN\r\n            ", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.LockAge, (object)SqlParameterName.LockCookie, (object)SqlParameterName.Locked, (object)SqlParameterName.ActionFlags, (object)SqlParameterName.SessionId, (object)SqlSessionStateRepositoryUtil.TableName);
        private static readonly string GetStateItemSql = string.Format("\r\n            BEGIN TRAN\r\n                DECLARE @textptr AS varbinary(16)\r\n                DECLARE @length AS int\r\n                DECLARE @now AS datetime\r\n                SET @now = GETUTCDATE()\r\n\r\n                UPDATE {0} WITH (XLOCK, ROWLOCK)\r\n                SET Expires = DATEADD(n, Timeout, @now), \r\n                    @{1} = Locked,\r\n                    @{2} = DATEDIFF(second, LockDate, @now),\r\n                    @{3} = LockCookie,\r\n                    @textptr = CASE @locked\r\n                        WHEN 0 THEN TEXTPTR(SessionItemLong)\r\n                        ELSE NULL\r\n                        END,\r\n                    @length = CASE @locked\r\n                        WHEN 0 THEN DATALENGTH(SessionItemLong)\r\n                        ELSE NULL\r\n                        END,\r\n\r\n                    /* If the Uninitialized flag (0x1) if it is set,\r\n                       remove it and return InitializeItem (0x1) in actionFlags */\r\n                    Flags = CASE\r\n                        WHEN (Flags & 1) <> 0 THEN (Flags & ~1)\r\n                        ELSE Flags\r\n                        END,\r\n                    @{4} = CASE\r\n                        WHEN (Flags & 1) <> 0 THEN 1\r\n                        ELSE 0\r\n                        END\r\n                WHERE SessionId = @{5}\r\n                IF @length IS NOT NULL BEGIN\r\n                    READTEXT {6}.SessionItemLong @textptr 0 @length\r\n                END\r\n            COMMIT TRAN\r\n              ", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.Locked, (object)SqlParameterName.LockAge, (object)SqlParameterName.LockCookie, (object)SqlParameterName.ActionFlags, (object)SqlParameterName.SessionId, (object)SqlSessionStateRepositoryUtil.TableName);
        private static readonly string ReleaseItemExclusiveSql = string.Format("\r\n            UPDATE {0}\r\n            SET Expires = DATEADD(n, Timeout, GETUTCDATE()),\r\n                Locked = 0\r\n            WHERE SessionId = @{1} AND LockCookie = @{2}", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.SessionId, (object)SqlParameterName.LockCookie);
        private static readonly string RemoveStateItemSql = string.Format("\r\n            DELETE {0}\r\n            WHERE SessionId = @{1} AND LockCookie = @{2}", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.SessionId, (object)SqlParameterName.LockCookie);
        private static readonly string ResetItemTimeoutSql = string.Format("\r\n            UPDATE {0}\r\n            SET Expires = DATEADD(n, Timeout, GETUTCDATE())\r\n            WHERE SessionId = @{1}", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.SessionId);
        private static readonly string UpdateStateItemLongSql = string.Format("\r\n            UPDATE {0} WITH (ROWLOCK)\r\n            SET Expires = DATEADD(n, @{1}, GETUTCDATE()), \r\n                SessionItemLong = @{2},\r\n                Timeout = @{3},\r\n                Locked = 0\r\n            WHERE SessionId = @{4} AND LockCookie = @{5}", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.Timeout, (object)SqlParameterName.SessionItemLong, (object)SqlParameterName.Timeout, (object)SqlParameterName.SessionId, (object)SqlParameterName.LockCookie);
        private static readonly string InsertStateItemLongSql = string.Format("\r\n            DECLARE @now AS datetime\r\n            DECLARE @nowLocal AS datetime\r\n            \r\n            SET @now = GETUTCDATE()\r\n            SET @nowLocal = GETDATE()\r\n\r\n            INSERT {0} \r\n                (SessionId, \r\n                 SessionItemLong, \r\n                 Timeout, \r\n                 Expires, \r\n                 Locked, \r\n                 LockDate,\r\n                 LockDateLocal,\r\n                 LockCookie) \r\n            VALUES \r\n                (@{1}, \r\n                 @{2}, \r\n                 @{3}, \r\n                 DATEADD(n, @{4}, @now), \r\n                 0, \r\n                 @now,\r\n                 @nowLocal,\r\n                 1)", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.SessionId, (object)SqlParameterName.SessionItemLong, (object)SqlParameterName.Timeout, (object)SqlParameterName.Timeout);
        private static readonly string DeleteExpiredSessionsSql = string.Format("\r\n            SET NOCOUNT ON\r\n            SET DEADLOCK_PRIORITY LOW\r\n\r\n            DECLARE @now datetime\r\n            SET @now = GETUTCDATE() \r\n\r\n            CREATE TABLE #tblExpiredSessions \r\n            ( \r\n                SessionId nvarchar({0}) NOT NULL PRIMARY KEY\r\n            )\r\n\r\n            INSERT #tblExpiredSessions (SessionId)\r\n                SELECT SessionId\r\n                FROM {1} WITH (READUNCOMMITTED)\r\n                WHERE Expires < @now\r\n\r\n            IF @@ROWCOUNT <> 0 \r\n            BEGIN \r\n                DECLARE ExpiredSessionCursor CURSOR LOCAL FORWARD_ONLY READ_ONLY\r\n                FOR SELECT SessionId FROM #tblExpiredSessions\r\n\r\n                DECLARE @SessionId nvarchar({2})\r\n\r\n                OPEN ExpiredSessionCursor\r\n\r\n                FETCH NEXT FROM ExpiredSessionCursor INTO @SessionId\r\n\r\n                WHILE @@FETCH_STATUS = 0 \r\n                    BEGIN\r\n                        DELETE FROM {3} WHERE SessionId = @SessionId AND Expires < @now\r\n                        FETCH NEXT FROM ExpiredSessionCursor INTO @SessionId\r\n                    END\r\n\r\n                CLOSE ExpiredSessionCursor\r\n\r\n                DEALLOCATE ExpiredSessionCursor\r\n            END \r\n\r\n            DROP TABLE #tblExpiredSessions", (object)SqlSessionStateRepositoryUtil.IdLength, (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlSessionStateRepositoryUtil.IdLength, (object)SqlSessionStateRepositoryUtil.TableName);
        private const int DEFAULT_RETRY_INTERVAL = 1000;
        private const int DEFAULT_RETRY_NUM = 10;
        private int _retryIntervalMilSec;
        private string _connectString;
        private int _maxRetryNum;
        private NpgsqlCommandHelper _commandHelper;

        public PostgresSessionStateRepository(string connectionString, int commandTimeout, int? retryInterval = 1000, int? retryNum = 10)
        {
            this._retryIntervalMilSec = retryInterval.HasValue ? retryInterval.Value : 1000;
            this._connectString = connectionString;
            this._maxRetryNum = retryNum.HasValue ? retryNum.Value : 10;
            this._commandHelper = new NpgsqlCommandHelper(commandTimeout);
        }

        private bool CanRetry(RetryCheckParameter parameter)
        {
            if (this._retryIntervalMilSec <= 0 || !SqlSessionStateRepositoryUtil.IsFatalSqlException(parameter.Exception) || parameter.RetryCount >= this._maxRetryNum)
                return false;
            Thread.Sleep(this._retryIntervalMilSec);
            ++parameter.RetryCount;
            return true;
        }

        public void CreateSessionStateTable()
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(this._connectString))
            {
                try
                {
                    NpgsqlCommand newSessionTableCmd = this._commandHelper.CreateNewSessionTableCmd(
                        PostgresSessionStateRepository.CreateSessionTableSql);
                    ConfiguredTaskAwaitable<int> configuredTaskAwaitable = SqlSessionStateRepositoryUtil.SqlExecuteNonQueryWithRetryAsync(connection, newSessionTableCmd, new Func<RetryCheckParameter, bool>(this.CanRetry), false).ConfigureAwait(false);
                    // ISSUE: explicit reference operation
                    ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter awaiter = ((ConfiguredTaskAwaitable<int>)@configuredTaskAwaitable).GetAwaiter();
                    // ISSUE: explicit reference operation
                    ((ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter)@awaiter).GetResult();
                }
                catch (Exception ex)
                {
                    throw new HttpException(Resource1.Cant_connect_sql_session_database, ex);
                }
            }
        }

        public void DeleteExpiredSessions()
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(this._connectString))
            {
                NpgsqlCommand expiredSessionsCmd = this._commandHelper.CreateDeleteExpiredSessionsCmd(PostgresSessionStateRepository.DeleteExpiredSessionsSql);
                ConfiguredTaskAwaitable<int> configuredTaskAwaitable = SqlSessionStateRepositoryUtil.SqlExecuteNonQueryWithRetryAsync(connection, expiredSessionsCmd, new Func<RetryCheckParameter, bool>(this.CanRetry), false).ConfigureAwait(false);
                // ISSUE: explicit reference operation
                ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter awaiter = ((ConfiguredTaskAwaitable<int>)@configuredTaskAwaitable).GetAwaiter();
                // ISSUE: explicit reference operation
                ((ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter)@awaiter).GetResult();
            }
        }

        public async Task<SessionItem> GetSessionStateItemAsync(string id, bool exclusive)
        {
            TimeSpan lockAge = TimeSpan.Zero;
            DateTime utcNow = DateTime.UtcNow;
            byte[] buf = (byte[])null;
            SessionStateActions actions = SessionStateActions.None;
            NpgsqlCommand cmd = (NpgsqlCommand)null;
            cmd = !exclusive ? this._commandHelper.CreateGetStateItemCmd(PostgresSessionStateRepository.GetStateItemSql, id) : this._commandHelper.CreateGetStateItemExclusiveCmd(PostgresSessionStateRepository.GetStateItemExclusiveSql, id);
            using (NpgsqlConnection connection = new NpgsqlConnection(this._connectString))
            {
                SqlDataReader reader = (SqlDataReader)await SqlSessionStateRepositoryUtil.SqlExecuteReaderWithRetryAsync(connection, cmd, new Func<RetryCheckParameter, bool>(this.CanRetry), CommandBehavior.Default);
                try
                {
                    if (reader.ReadAsync().Result)
                        buf = (byte[])await (Task<byte[]>)reader.GetFieldValueAsync<byte[]>(0);
                }
                finally
                {
                    if (reader != null)
                        reader.Dispose();
                }
                reader = (SqlDataReader)null;
                NpgsqlParameter putParameterValue = cmd.GetOutPutParameterValue(SqlParameterName.Locked);
                if (putParameterValue == null || Convert.IsDBNull(putParameterValue.Value))
                    return (SessionItem)null;
                int num = (bool)putParameterValue.Value ? 1 : 0;
                object lockId = (object)(int)cmd.GetOutPutParameterValue(SqlParameterName.LockCookie).Value;
                if (num != 0)
                {
                    lockAge = new TimeSpan(0, 0, (int)cmd.GetOutPutParameterValue(SqlParameterName.LockAge).Value);
                    if (lockAge > new TimeSpan(0, 0, 31536000))
                        lockAge = TimeSpan.Zero;
                    return new SessionItem((byte[])null, true, lockAge, lockId, actions);
                }
                actions = (SessionStateActions)cmd.GetOutPutParameterValue(SqlParameterName.ActionFlags).Value;
                if (buf == null)
                    buf = (byte[])cmd.GetOutPutParameterValue(SqlParameterName.SessionItemLong).Value;
                return new SessionItem(buf, true, lockAge, lockId, actions);
            }
        }

        public async Task CreateOrUpdateSessionStateItemAsync(bool newItem, string id, byte[] buf, int length, int timeout, int lockCookie, int orginalStreamLen)
        {
            NpgsqlCommand sqlCmd = newItem ? this._commandHelper.CreateInsertStateItemLongCmd(PostgresSessionStateRepository.InsertStateItemLongSql, id, buf, length, timeout) : this._commandHelper.CreateUpdateStateItemLongCmd(PostgresSessionStateRepository.UpdateStateItemLongSql, id, buf, length, timeout, lockCookie);
            NpgsqlConnection connection = new NpgsqlConnection(this._connectString);
            try
            {
                int num = await SqlSessionStateRepositoryUtil.SqlExecuteNonQueryWithRetryAsync(connection, sqlCmd, new Func<RetryCheckParameter, bool>(this.CanRetry), newItem);
            }
            finally
            {
                if (connection != null)
                    connection.Dispose();
            }
            connection = (NpgsqlConnection)null;
        }

        public async Task ResetSessionItemTimeoutAsync(string id)
        {
            NpgsqlCommand resetItemTimeoutCmd = this._commandHelper.CreateResetItemTimeoutCmd(PostgresSessionStateRepository.ResetItemTimeoutSql, id);
            NpgsqlConnection connection = new NpgsqlConnection(this._connectString);
            try
            {
                int num = await SqlSessionStateRepositoryUtil.SqlExecuteNonQueryWithRetryAsync(connection, resetItemTimeoutCmd, new Func<RetryCheckParameter, bool>(this.CanRetry), false);
            }
            finally
            {
                if (connection != null)
                    connection.Dispose();
            }
            connection = (NpgsqlConnection)null;
        }

        public async Task RemoveSessionItemAsync(string id, object lockId)
        {
            NpgsqlCommand removeStateItemCmd = this._commandHelper.CreateRemoveStateItemCmd(PostgresSessionStateRepository.RemoveStateItemSql, id, lockId);
            NpgsqlConnection connection = new NpgsqlConnection(this._connectString);
            try
            {
                int num = await SqlSessionStateRepositoryUtil.SqlExecuteNonQueryWithRetryAsync(connection, removeStateItemCmd, new Func<RetryCheckParameter, bool>(this.CanRetry), false);
            }
            finally
            {
                if (connection != null)
                    connection.Dispose();
            }
            connection = (NpgsqlConnection)null;
        }

        public async Task ReleaseSessionItemAsync(string id, object lockId)
        {
            NpgsqlCommand itemExclusiveCmd = this._commandHelper.CreateReleaseItemExclusiveCmd(PostgresSessionStateRepository.ReleaseItemExclusiveSql, id, lockId);
            NpgsqlConnection connection = new NpgsqlConnection(this._connectString);
            try
            {
                int num = await SqlSessionStateRepositoryUtil.SqlExecuteNonQueryWithRetryAsync(connection, itemExclusiveCmd, new Func<RetryCheckParameter, bool>(this.CanRetry), false);
            }
            finally
            {
                if (connection != null)
                    connection.Dispose();
            }
            connection = (NpgsqlConnection)null;
        }

        public async Task CreateUninitializedSessionItemAsync(string id, int length, byte[] buf, int timeout)
        {

            NpgsqlCommand uninitializedItemCmd = this._commandHelper.CreateTempInsertUninitializedItemCmd(PostgresSessionStateRepository.TempInsertUninitializedItemSql, id, length, buf, timeout);
            NpgsqlConnection connection = new NpgsqlConnection(this._connectString);
            try
            {
                int num = await SqlSessionStateRepositoryUtil.SqlExecuteNonQueryWithRetryAsync(connection, uninitializedItemCmd, new Func<RetryCheckParameter, bool>(this.CanRetry), true);
            }
            finally
            {
                if (connection != null)
                    connection.Dispose();
            }
            connection = (NpgsqlConnection)null;
        }
    }

}
