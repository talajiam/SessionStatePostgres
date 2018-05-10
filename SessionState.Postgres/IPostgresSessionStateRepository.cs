using System.Threading.Tasks;

namespace SessionState.Postgres
{
    internal interface IPostgresSessionStateRepository
    {
        void CreateSessionStateTable();

        void DeleteExpiredSessions();

        Task<SessionItem> GetSessionStateItemAsync(string id, bool exclusive);

        Task CreateOrUpdateSessionStateItemAsync(bool newItem, string id, byte[] buf, int length, int timeout, int lockCookie, int orginalStreamLen);

        Task ResetSessionItemTimeoutAsync(string id);

        Task RemoveSessionItemAsync(string id, object lockId);

        Task ReleaseSessionItemAsync(string id, object lockId);

        Task CreateUninitializedSessionItemAsync(string id, int length, byte[] buf, int timeout);
    }
}
