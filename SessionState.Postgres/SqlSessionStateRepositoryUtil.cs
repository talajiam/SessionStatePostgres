using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Npgsql;

namespace SessionState.Postgres
{
    internal class SqlSessionStateRepositoryUtil
    {
        public static readonly string TableName = "ASPStateTempSessions";
        public static readonly int IdLength = 88;
        public static readonly int DefaultItemLength = 7000;
        private const int ITEM_SHORT_LENGTH = 7000;
        private const int SQL_ERROR_PRIMARY_KEY_VIOLATION = 2627;
        private const int SQL_LOGIN_FAILED = 18456;
        private const int SQL_LOGIN_FAILED_2 = 18452;
        private const int SQL_LOGIN_FAILED_3 = 18450;
        private const int SQL_CANNOT_OPEN_DATABASE_FOR_LOGIN = 4060;
        private const int SQL_TIMEOUT_EXPIRED = -2;
        private const int APP_SUFFIX_LENGTH = 8;

        

        public static async Task<int> SqlExecuteNonQueryWithRetryAsync(NpgsqlConnection connection, NpgsqlCommand sqlCmd, Func<RetryCheckParameter, bool> canRetry, bool ignoreInsertPKException = false)
        {
            RetryCheckParameter retryParamenter = new RetryCheckParameter()
            {
                EndRetryTime = DateTime.UtcNow,
                RetryCount = 0
            };
            sqlCmd.Connection = connection;
            label_1:
            try
            {
                await SqlSessionStateRepositoryUtil.OpenConnectionAsync(connection);
                return (int)await sqlCmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex)
            {
                if (SqlSessionStateRepositoryUtil.IsInsertPKException(ex, ignoreInsertPKException))
                    return -1;
                retryParamenter.Exception = ex;
                if (!canRetry(retryParamenter))
                    throw;
                else
                    goto label_1;
            }
        }

        public static async Task<SqlDataReader> SqlExecuteReaderWithRetryAsync(NpgsqlConnection connection, NpgsqlCommand sqlCmd, Func<RetryCheckParameter, bool> canRetry, CommandBehavior cmdBehavior = CommandBehavior.Default)
        {
            RetryCheckParameter retryParamenter = new RetryCheckParameter()
            {
                EndRetryTime = DateTime.UtcNow,
                RetryCount = 0
            };
            sqlCmd.Connection = connection;
            label_1:
            SqlDataReader sqlDataReader;
            try
            {
                await SqlSessionStateRepositoryUtil.OpenConnectionAsync(connection);
                sqlDataReader = (SqlDataReader)await sqlCmd.ExecuteReaderAsync(cmdBehavior);
            }
            catch (SqlException ex)
            {
                retryParamenter.Exception = ex;
                if (!canRetry(retryParamenter))
                    throw;
                else
                    goto label_1;
            }
            return sqlDataReader;
        }

        public static bool IsFatalSqlException(SqlException ex)
        {
            return ex != null && ((int)ex.Class >= 20 || ex.Number == 4060 || ex.Number == -2);
        }

        private static async Task OpenConnectionAsync(NpgsqlConnection NpgsqlConnection)
        {
            try
            {
                if (NpgsqlConnection.State == ConnectionState.Open)
                    return;
                await NpgsqlConnection.OpenAsync();
            }
            catch (NpgsqlException ex)
            {

                throw new HttpException(string.Format(Resource1.Login_failed_sql_session_database));

                //if (ex != null && (ex.Number == 18456 || ex.Number == 18452 || ex.Number == 18450))
                //{
                //    NpgsqlConnectionStringBuilder connectionStringBuilder = new NpgsqlConnectionStringBuilder(NpgsqlConnection.ConnectionString);
                //    throw new HttpException(string.Format(Resource1.Login_failed_sql_session_database,
                //        !connectionStringBuilder.IntegratedSecurity ? (object)connectionStringBuilder.UserID : (object)((ClaimsIdentity)WindowsIdentity.GetCurrent()).Name), (Exception)ex);
                //}
            }
            catch (Exception ex)
            {
                throw new HttpException(Resource1.Cant_connect_sql_session_database, ex);
            }
        }

        private static bool IsInsertPKException(SqlException ex, bool ignoreInsertPKException)
        {
            return ((ex == null ? 0 : (ex.Number == 2627 ? 1 : 0)) & (ignoreInsertPKException ? 1 : 0)) != 0;
        }
    }

}
