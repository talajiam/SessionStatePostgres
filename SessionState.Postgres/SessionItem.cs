using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.SessionState;

namespace SessionState.Postgres
{
    internal class SessionItem
    {
        public SessionItem(byte[] item, bool locked, TimeSpan lockAge, object lockId, SessionStateActions actions)
        {
            this.Item = item;
            this.Locked = locked;
            this.LockAge = lockAge;
            this.LockId = lockId;
            this.Actions = actions;
        }

        public byte[] Item { get; private set; }

        public bool Locked { get; private set; }

        public TimeSpan LockAge { get; private set; }

        public object LockId { get; private set; }

        public SessionStateActions Actions { get; private set; }
    }
}
