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
    internal class SqlInMemoryTableSessionStateRepository : IPostgresSessionStateRepository
    {
        private static readonly string CreateSessionTableSql = string.Format("\r\n               IF NOT EXISTS (SELECT * \r\n                 FROM INFORMATION_SCHEMA.TABLES \r\n                 WHERE TABLE_NAME = '{0}')\r\n               BEGIN\r\n                CREATE TABLE {1} (\r\n                SessionId           nvarchar(88)    COLLATE Latin1_General_100_BIN2 NOT NULL,\r\n                Created             datetime        NOT NULL DEFAULT GETUTCDATE(),\r\n                Expires             datetime        NOT NULL,\r\n                LockDate            datetime        NOT NULL,\r\n                LockDateLocal       datetime        NOT NULL,\r\n                LockCookie          int             NOT NULL,\r\n                Timeout             int             NOT NULL,\r\n                Locked              bit             NOT NULL,\r\n                SessionItemLong     varbinary(max)           NULL,\r\n                Flags               int             NOT NULL DEFAULT 0,\r\n                INDEX [Index_Expires] NONCLUSTERED \r\n                (\r\n\t                [Expires] ASC\r\n                ),\r\n                PRIMARY KEY NONCLUSTERED HASH \r\n                (\r\n\t                [SessionId]\r\n                )WITH ( BUCKET_COUNT = 33554432)\r\n                )WITH ( MEMORY_OPTIMIZED = ON , DURABILITY = SCHEMA_ONLY )                \r\n              END", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlSessionStateRepositoryUtil.TableName);
        private static readonly string GetStateItemExclusiveSql = string.Format("\r\n                BEGIN TRAN\r\n                    DECLARE @textptr AS varbinary(max)\r\n                    DECLARE @length AS int\r\n                    DECLARE @now AS datetime\r\n                    DECLARE @nowLocal AS datetime\r\n\r\n                    SET @now = GETUTCDATE()\r\n                    SET @nowLocal = GETDATE()\r\n\r\n                    DECLARE @LockedCheck bit\r\n                    DECLARE @Flags int\r\n\r\n                    SELECT @LockedCheck = Locked, @Flags = Flags \r\n                        FROM {0}\r\n                        WHERE SessionID = @{1}\r\n                    IF @Flags&1 <> 0\r\n                    BEGIN\r\n                        SET @actionFlags = 1\r\n                        UPDATE {2}\r\n                            SET Flags = Flags & ~1 WHERE SessionID = @{3}\r\n                    END\r\n                    ELSE\r\n                        SET @{4} = 0\r\n\r\n                    IF @LockedCheck = 1\r\n                    BEGIN\r\n                        UPDATE {5}\r\n                        SET Expires = DATEADD(n, Timeout, @now), \r\n                            @{6} = DATEDIFF(second, LockDate, @now),\r\n                            @{7} = LockCookie,\r\n                            --@textptr = NULL,\r\n                            @length = NULL,\r\n                            @{8} = 1\r\n                        WHERE SessionId = @{9}\r\n                    END\r\n                    ELSE\r\n                    BEGIN\r\n                        UPDATE {10}\r\n                        SET Expires = DATEADD(n, Timeout, @now), \r\n                            LockDate = @now,\r\n                            LockDateLocal = @nowlocal,\r\n                            @{11} = 0,\r\n                            @{12} = LockCookie = LockCookie + 1,\r\n                            @textptr = SessionItemLong,\r\n                            @length = 1,\r\n                            @{13} = 0,\r\n                            Locked = 1\r\n                        WHERE SessionId = @{14}\r\n\r\n                        IF @TextPtr IS NOT NULL\r\n                            SELECT @TextPtr\r\n                    END\r\n                COMMIT TRAN", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.SessionId, (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.SessionId, (object)SqlParameterName.ActionFlags, (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.LockAge, (object)SqlParameterName.LockCookie, (object)SqlParameterName.Locked, (object)SqlParameterName.SessionId, (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.LockAge, (object)SqlParameterName.LockCookie, (object)SqlParameterName.Locked, (object)SqlParameterName.SessionId);
        private static readonly string GetStateItemSql = string.Format("\r\n                BEGIN TRAN\r\n                    DECLARE @textptr AS varbinary(max)\r\n                    DECLARE @length AS int\r\n                    DECLARE @now AS datetime\r\n                    SET @now = GETUTCDATE()\r\n\r\n                    UPDATE {0}\r\n                    SET Expires = DATEADD(n, Timeout, @now), \r\n                        @{1} = Locked,\r\n                        @{2} = DATEDIFF(second, LockDate, @now),\r\n                        @{3} = LockCookie,                   \r\n                        @textptr = CASE @{4}\r\n                            WHEN 0 THEN SessionItemLong\r\n                            ELSE NULL\r\n                            END,\r\n                        @length = CASE @{5}\r\n                            WHEN 0 THEN DATALENGTH(SessionItemLong)\r\n                            ELSE NULL\r\n                            END,\r\n                            /* If the Uninitialized flag (0x1) if it is set,\r\n                            remove it and return InitializeItem (0x1) in actionFlags */\r\n                        Flags = CASE\r\n                            WHEN (Flags & 1) <> 0 THEN (Flags & ~1)\r\n                            ELSE Flags\r\n                            END,\r\n                        @{6} = CASE\r\n                            WHEN (Flags & 1) <> 0 THEN 1\r\n                            ELSE 0\r\n                            END\r\n                    WHERE SessionId = @{7}\r\n                    IF @length IS NOT NULL BEGIN\r\n                        SELECT @textptr\r\n                    END\r\n            COMMIT TRAN\r\n            ", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.Locked, (object)SqlParameterName.LockAge, (object)SqlParameterName.LockCookie, (object)SqlParameterName.Locked, (object)SqlParameterName.Locked, (object)SqlParameterName.ActionFlags, (object)SqlParameterName.SessionId);
        private static readonly string DeleteExpiredSessionsSql = string.Format("\r\n                SET NOCOUNT ON\r\n                SET DEADLOCK_PRIORITY LOW \r\n\r\n                DECLARE @now datetime\r\n                SET @now = GETUTCDATE() \r\n\r\n                CREATE TABLE #tblExpiredSessions \r\n                ( \r\n                    SessionId nvarchar({0}) NOT NULL PRIMARY KEY\r\n                )\r\n\r\n                INSERT #tblExpiredSessions (SessionId)\r\n                    SELECT SessionId\r\n                    FROM {1} WITH (SNAPSHOT)\r\n                    WHERE Expires < @now\r\n\r\n                IF @@ROWCOUNT <> 0 \r\n                BEGIN \r\n                    DECLARE ExpiredSessionCursor CURSOR LOCAL FORWARD_ONLY READ_ONLY\r\n                    FOR SELECT SessionId FROM #tblExpiredSessions \r\n\r\n                    DECLARE @SessionId nvarchar({2})\r\n\r\n                    OPEN ExpiredSessionCursor\r\n\r\n                    FETCH NEXT FROM ExpiredSessionCursor INTO @SessionId\r\n\r\n                    WHILE @@FETCH_STATUS = 0 \r\n                        BEGIN\r\n                            DELETE FROM {3} WHERE SessionId = @SessionId AND Expires < @now\r\n                            FETCH NEXT FROM ExpiredSessionCursor INTO @SessionId\r\n                        END\r\n\r\n                    CLOSE ExpiredSessionCursor\r\n\r\n                    DEALLOCATE ExpiredSessionCursor\r\n\r\n                END \r\n\r\n                DROP TABLE #tblExpiredSessions", (object)SqlSessionStateRepositoryUtil.IdLength, (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlSessionStateRepositoryUtil.IdLength, (object)SqlSessionStateRepositoryUtil.TableName);
        private static readonly string TempInsertUninitializedItemSql = string.Format("\r\n            DECLARE @now AS datetime\r\n            DECLARE @nowLocal AS datetime\r\n            SET @now = GETUTCDATE()\r\n            SET @nowLocal = GETDATE()\r\n\r\n            INSERT {0} (SessionId, \r\n                 SessionItemLong,\r\n                 Timeout, \r\n                 Expires, \r\n                 Locked, \r\n                 LockDate,\r\n                 LockDateLocal,\r\n                 LockCookie,\r\n                 Flags) \r\n            VALUES\r\n                (@{1},\r\n                 @{2},\r\n                 @{3},\r\n                 DATEADD(n, @{4}, @now),\r\n                 0,\r\n                 @now,\r\n                 @nowLocal,\r\n                 1,\r\n                 1)", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.SessionId, (object)SqlParameterName.SessionItemLong, (object)SqlParameterName.Timeout, (object)SqlParameterName.Timeout);
        private static readonly string ReleaseItemExclusiveSql = string.Format("\r\n            UPDATE {0}\r\n            SET Expires = DATEADD(n, Timeout, GETUTCDATE()),\r\n                Locked = 0\r\n            WHERE SessionId = @{1} AND LockCookie = @{2}", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.SessionId, (object)SqlParameterName.LockCookie);
        private static readonly string RemoveStateItemSql = string.Format("\r\n            DELETE {0}\r\n            WHERE SessionId = @{1} AND LockCookie = @{2}", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.SessionId, (object)SqlParameterName.LockCookie);
        private static readonly string ResetItemTimeoutSql = string.Format("\r\n            UPDATE {0}\r\n            SET Expires = DATEADD(n, Timeout, GETUTCDATE())\r\n            WHERE SessionId = @{1}", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.SessionId);
        private static readonly string UpdateStateItemLongSql = string.Format("\r\n            UPDATE {0}\r\n            SET Expires = DATEADD(n, @{1}, GETUTCDATE()), \r\n                SessionItemLong = @{2},\r\n                Timeout = @{3},\r\n                Locked = 0\r\n            WHERE SessionId = @{4} AND LockCookie = @{5}", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.Timeout, (object)SqlParameterName.SessionItemLong, (object)SqlParameterName.Timeout, (object)SqlParameterName.SessionId, (object)SqlParameterName.LockCookie);
        private static readonly string InsertStateItemLongSql = string.Format("\r\n            DECLARE @now AS datetime\r\n            DECLARE @nowLocal AS datetime\r\n            \r\n            SET @now = GETUTCDATE()\r\n            SET @nowLocal = GETDATE()\r\n\r\n            INSERT {0} \r\n                (SessionId, \r\n                 SessionItemLong, \r\n                 Timeout, \r\n                 Expires, \r\n                 Locked, \r\n                 LockDate,\r\n                 LockDateLocal,\r\n                 LockCookie) \r\n            VALUES \r\n                (@{1}, \r\n                 @{2}, \r\n                 @{3}, \r\n                 DATEADD(n, @{4}, @now), \r\n                 0, \r\n                 @now,\r\n                 @nowLocal,\r\n                 1)", (object)SqlSessionStateRepositoryUtil.TableName, (object)SqlParameterName.SessionId, (object)SqlParameterName.SessionItemLong, (object)SqlParameterName.Timeout, (object)SqlParameterName.Timeout);
        private const int DEFAULT_RETRY_NUM = 10;
        private const int DEFAULT_RETRY_INERVAL = 1;
        private int _retryIntervalMilSec;
        private string _connectString;
        private int _maxRetryNum;
        private NpgsqlCommandHelper _commandHelper;

        public SqlInMemoryTableSessionStateRepository(string connectionString, int commandTimeout, int? retryInterval, int? retryNum)
        {
            this._retryIntervalMilSec = retryInterval.HasValue ? retryInterval.Value : 1;
            this._connectString = connectionString;
            this._maxRetryNum = retryNum.HasValue ? retryNum.Value : 10;
            this._commandHelper = new NpgsqlCommandHelper(commandTimeout);
        }

        public void CreateSessionStateTable()
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(this._connectString))
            {
                try
                {
                    NpgsqlCommand newSessionTableCmd = this._commandHelper.CreateNewSessionTableCmd(SqlInMemoryTableSessionStateRepository.CreateSessionTableSql);
                    ConfiguredTaskAwaitable<int> configuredTaskAwaitable = SqlSessionStateRepositoryUtil.SqlExecuteNonQueryWithRetryAsync(connection, newSessionTableCmd, new Func<RetryCheckParameter, bool>(this.CanRetry), false).ConfigureAwait(false);
                    // ISSUE: explicit reference operation
                    ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter awaiter = ((ConfiguredTaskAwaitable<int>)@configuredTaskAwaitable).GetAwaiter();
                    // ISSUE: explicit reference operation
                    ((ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter)@awaiter).GetResult();
                }
                catch (Exception ex)
                {
                    SqlException sqlException = ex as SqlException;
                    if (sqlException != null && (sqlException.Number == 40536 || sqlException.Number == 41337))
                        throw sqlException;
                    throw new HttpException(Resource1.Cant_connect_sql_session_database, ex);
                }
            }
        }

        public void DeleteExpiredSessions()
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(this._connectString))
            {
                NpgsqlCommand expiredSessionsCmd = this._commandHelper.CreateDeleteExpiredSessionsCmd(SqlInMemoryTableSessionStateRepository.DeleteExpiredSessionsSql);
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
            cmd = !exclusive ? this._commandHelper.CreateGetStateItemCmd(SqlInMemoryTableSessionStateRepository.GetStateItemSql, id) : this._commandHelper.CreateGetStateItemExclusiveCmd(SqlInMemoryTableSessionStateRepository.GetStateItemExclusiveSql, id);
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
                return new SessionItem(buf, true, lockAge, lockId, actions);
            }
        }

        public async Task CreateOrUpdateSessionStateItemAsync(bool newItem, string id, byte[] buf, int length, int timeout, int lockCookie, int orginalStreamLen)
        {
            NpgsqlCommand sqlCmd = newItem ? this._commandHelper.CreateInsertStateItemLongCmd(SqlInMemoryTableSessionStateRepository.InsertStateItemLongSql, id, buf, length, timeout) : this._commandHelper.CreateUpdateStateItemLongCmd(SqlInMemoryTableSessionStateRepository.UpdateStateItemLongSql, id, buf, length, timeout, lockCookie);
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
            NpgsqlCommand resetItemTimeoutCmd = this._commandHelper.CreateResetItemTimeoutCmd(SqlInMemoryTableSessionStateRepository.ResetItemTimeoutSql, id);
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
            NpgsqlCommand removeStateItemCmd = this._commandHelper.CreateRemoveStateItemCmd(SqlInMemoryTableSessionStateRepository.RemoveStateItemSql, id, lockId);
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
            NpgsqlCommand itemExclusiveCmd = this._commandHelper.CreateReleaseItemExclusiveCmd(SqlInMemoryTableSessionStateRepository.ReleaseItemExclusiveSql, id, lockId);
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
            NpgsqlCommand uninitializedItemCmd = this._commandHelper.CreateTempInsertUninitializedItemCmd(SqlInMemoryTableSessionStateRepository.TempInsertUninitializedItemSql, id, length, buf, timeout);
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

        private bool CanRetry(RetryCheckParameter parameter)
        {
            if (parameter.RetryCount >= this._maxRetryNum || !this.ShouldUseInMemoryTableRetry(parameter.Exception))
                return false;
            Thread.Sleep(this._retryIntervalMilSec);
            ++parameter.RetryCount;
            return true;
        }

        private bool ShouldUseInMemoryTableRetry(SqlException ex)
        {
            return ex != null && (ex.Number == 41302 || ex.Number == 41305 || (ex.Number == 41325 || ex.Number == 41301) || ex.Number == 41839);
        }
    }
}
