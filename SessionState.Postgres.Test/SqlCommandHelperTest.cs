﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NpgsqlTypes;
using SessionState.Postgres;
namespace SessionState.Postgres.Test
{
    using Npgsql;
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using Xunit;

    public class SqlCommandHelperTest
    {
        private const int SqlCommandTimeout = 10;
        private const string SessionId = "testid";
        private const string SqlStatement = "moq sql statement";
        private static readonly byte[] Buffer = new byte[BufferLength];
        private const int BufferLength = 123;
        private const int SessionTimeout = 120;
        private const int LockId = 1;

        [Fact]
        public void Constructor_Should_Initialize_CommandTimeout()
        {
            var helper = new NpgsqlCommandHelper(SqlCommandTimeout);

            Assert.Equal(SqlCommandTimeout, helper.CommandTimeout);
        }

        [Fact]
        public void CreateNewSessionTableCmd_Should_Create_SqlCommand_Without_Parameters()
        {
            var helper = new NpgsqlCommandHelper(SqlCommandTimeout);

            var cmd = helper.CreateNewSessionTableCmd(SqlStatement);

            VerifyBasicsOfSqlCommand(cmd);
            Assert.Empty(cmd.Parameters);
        }

        [Fact]
        public void CreateGetStateItemExclusiveCmd_Should_Create_SqlCommand_With_Right_Parameters()
        {
            var helper = new NpgsqlCommandHelper(SqlCommandTimeout);

            var cmd = helper.CreateGetStateItemExclusiveCmd(SqlStatement, SessionId);

            VerifyBasicsOfSqlCommand(cmd);
            VerifySessionIdParameter(cmd);
            //VerifyLockAgeParameter(cmd);
            //VerifyLockedParameter(cmd);
            //VerifyLockCookieParameter(cmd);
            //VerifyActionFlagsParameter(cmd);
            Assert.Equal(3, cmd.Parameters.Count);
        }

        [Fact]
        public void CreateGetStateItemCmd_Should_Create_SqlCommand_With_Right_Parameters()
        {
            var helper = new NpgsqlCommandHelper(SqlCommandTimeout);

            var cmd = helper.CreateGetStateItemCmd(SqlStatement, SessionId);

            VerifyBasicsOfSqlCommand(cmd);
            VerifySessionIdParameter(cmd);
            //VerifyLockedParameter(cmd);
            //VerifyLockAgeParameter(cmd);
            //VerifyLockCookieParameter(cmd);
            //VerifyActionFlagsParameter(cmd);
            Assert.Equal(2, cmd.Parameters.Count);
        }

        [Fact]
        public void CreateDeleteExpiredSessionsCmd_Should_Create_SqlCommand_Withone_Parameters()
        {
            var helper = new NpgsqlCommandHelper(SqlCommandTimeout);

            var cmd = helper.CreateDeleteExpiredSessionsCmd(SqlStatement);

            VerifyBasicsOfSqlCommand(cmd);
            Assert.Single(cmd.Parameters);
        }

        [Fact]
        public void CreateTempInsertUninitializedItemCmd_Should_Create_SqlCommand_With_Right_Parameters()
        {
            var helper = new NpgsqlCommandHelper(SqlCommandTimeout);

            var cmd = helper.CreateTempInsertUninitializedItemCmd(SqlStatement, SessionId, BufferLength, Buffer, SessionTimeout);

            VerifyBasicsOfSqlCommand(cmd);
            VerifySessionIdParameter(cmd);
            VerifySessionItemLongParameter(cmd);
            VerifyTimeoutParameter(cmd);
            //lockdate
            //lockdatelocal
            //expirestime
            Assert.Equal(6, cmd.Parameters.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(LockId)]
        public void CreateReleaseItemExclusiveCmd_Should_Create_SqlCommand_With_Right_Parameters(object lockId)
        {
            var helper = new NpgsqlCommandHelper(SqlCommandTimeout);

            var cmd = helper.CreateReleaseItemExclusiveCmd(SqlStatement, SessionId, lockId);

            VerifyBasicsOfSqlCommand(cmd);
            VerifySessionIdParameter(cmd);
            //lockdate
            //VerifyLockCookieParameter(cmd, lockId);
            Assert.Equal(2, cmd.Parameters.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(LockId)]
        public void CreateRemoveStateItemCmd_Should_Create_SqlCommand_With_Right_Parameters(object lockId)
        {
            var helper = new NpgsqlCommandHelper(SqlCommandTimeout);

            var cmd = helper.CreateRemoveStateItemCmd(SqlStatement, SessionId, lockId);

            VerifyBasicsOfSqlCommand(cmd);
            VerifySessionIdParameter(cmd);
        //    VerifyLockCookieParameter(cmd, lockId);
            Assert.Equal(1, cmd.Parameters.Count);
        }

        [Fact]
        public void CreateResetItemTimeoutCmd_Should_Create_SqlCommand_With_Right_Parameters()
        {
            var helper = new NpgsqlCommandHelper(SqlCommandTimeout);

            var cmd = helper.CreateResetItemTimeoutCmd(SqlStatement, SessionId);

            VerifyBasicsOfSqlCommand(cmd);
            VerifySessionIdParameter(cmd);
            //lockdate
            Assert.Equal(2, cmd.Parameters.Count);
        }

        [Fact]
        public void CreateUpdateStateItemLongCmd_Should_Create_SqlCommand_With_Right_Parameters()
        {
            var helper = new NpgsqlCommandHelper(SqlCommandTimeout);

            var cmd = helper.CreateUpdateStateItemLongCmd(SqlStatement, SessionId, Buffer, BufferLength, SessionTimeout, LockId);

            VerifyBasicsOfSqlCommand(cmd);
            VerifySessionIdParameter(cmd);
            VerifySessionItemLongParameter(cmd);
            VerifyTimeoutParameter(cmd);
            //lockdate
            //expiresin
            //VerifyLockCookieParameter(cmd, LockId);
            Assert.Equal(5, cmd.Parameters.Count);
        }

        [Fact]
        public void CreateInsertStateItemLongCmd_Should_Create_SqlCommand_With_Right_Parameters()
        {
            var helper = new NpgsqlCommandHelper(SqlCommandTimeout);

            var cmd = helper.CreateInsertStateItemLongCmd(SqlStatement, SessionId, Buffer, BufferLength, SessionTimeout);

            VerifyBasicsOfSqlCommand(cmd);
            VerifySessionIdParameter(cmd);
            VerifySessionItemLongParameter(cmd);
            VerifyTimeoutParameter(cmd);
            //lockdate
            //lockdatelocal
            //expirestime
            Assert.Equal(6, cmd.Parameters.Count);
        }

        private void VerifyBasicsOfSqlCommand(NpgsqlCommand cmd)
        {
            Assert.Equal(SqlStatement, cmd.CommandText);
            Assert.Equal(CommandType.Text, cmd.CommandType);
            Assert.Equal(SqlCommandTimeout, cmd.CommandTimeout);
        }

        private void VerifySessionIdParameter(NpgsqlCommand cmd)
        {
            var param = cmd.Parameters[$"@{SqlParameterName.SessionId}"];
            Assert.NotNull(param);
            Assert.Equal(NpgsqlDbType.Varchar, param.NpgsqlDbType);
            Assert.Equal(SessionId, param.Value);
            Assert.Equal(SqlSessionStateRepositoryUtil.IdLength, param.Size);
        }

        private void VerifyLockAgeParameter(NpgsqlCommand cmd)
        {
            var param = cmd.Parameters[$"@{SqlParameterName.LockAge}"];
            Assert.NotNull(param);
            Assert.Equal(NpgsqlDbType.Integer, param.NpgsqlDbType);
            Assert.Equal(Convert.DBNull, param.Value);
            Assert.Equal(ParameterDirection.Output, param.Direction);
        }

        private void VerifyLockedParameter(NpgsqlCommand cmd)
        {
            var param = cmd.Parameters[$"@{SqlParameterName.Locked}"];
            Assert.NotNull(param);
            Assert.Equal(NpgsqlDbType.Bit, param.NpgsqlDbType);
            Assert.Equal(Convert.DBNull, param.Value);
            Assert.Equal(ParameterDirection.Output, param.Direction);
        }

        private void VerifyLockCookieParameter(NpgsqlCommand cmd, object lockId = null)
        {
            var param = cmd.Parameters[$"@{SqlParameterName.LockCookie}"];
            Assert.NotNull(param);
            Assert.Equal(NpgsqlDbType.Integer, param.NpgsqlDbType);
            if (lockId == null)
            {
                Assert.Equal(Convert.DBNull, param.Value);
                Assert.Equal(ParameterDirection.Output, param.Direction);
            }
            else
            {
                Assert.Equal(lockId, param.Value);
            }
        }

        private void VerifyActionFlagsParameter(NpgsqlCommand cmd)
        {
            var param = cmd.Parameters[$"@{SqlParameterName.ActionFlags}"];
            Assert.NotNull(param);
            Assert.Equal(NpgsqlDbType.Integer, param.NpgsqlDbType);
            Assert.Equal(Convert.DBNull, param.Value);
            Assert.Equal(ParameterDirection.Output, param.Direction);
        }

        private void VerifySessionItemLongParameter(NpgsqlCommand cmd)
        {
            var param = cmd.Parameters[$"@{SqlParameterName.SessionItemLong}"];
            Assert.NotNull(param);
            Assert.Equal(NpgsqlDbType.Bytea, param.NpgsqlDbType);
            Assert.Equal(BufferLength, param.Size);
            Assert.Equal(Buffer, param.Value);
        }

        private void VerifyTimeoutParameter(NpgsqlCommand cmd)
        {
            var param = cmd.Parameters[$"@{SqlParameterName.Timeout}"];

            Assert.NotNull(param);
            Assert.Equal(NpgsqlDbType.Integer, param.NpgsqlDbType);
        }
    }
}
