using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace SampleProj1
{
    [Serializable]
    public class ToCheck1
    {
        public string S1 { get; set; }

        public int IntValue { get; set; }

        public decimal decValue { get; set; }

        public double doubleValue { get; set; }

        public bool booleanvalue { get; set; }
    }
    internal enum SqlParameterName
    {
        SessionId,
        Created,
        Expires,
        LockDate,
        LockDateLocal,
        LockCookie,
        Timeout,
        Locked,
        SessionItemLong,
        Flags,
        LockAge,
        ActionFlags,
    }
    class Program
    {
        static string SessionId1 = "9888333";
        static void Main(string[] args)
        {

            //NpgsqlConnection con = new NpgsqlConnection("Server = 127.0.0.1; User Id = postgres; Password=postgres;Database=sportswebtest;");
            //con.Open();
            //NpgsqlCommand cmd = new NpgsqlCommand("select * from aspstatetempsessions");
            //cmd.Connection = con;

            string id = "9888333";

            var itemtoInsert = new ToCheck1() { IntValue = 3, S1 = "My Check Value", booleanvalue = true, decValue = 3.90m, doubleValue = 8.409 };
            //            InsertRecord(itemtoInsert);
            UpdateAndRead();
            //ReadRecord();

        }

        static ToCheck1 ReadRecord()
        {
            NpgsqlConnection con = new NpgsqlConnection("Server = 127.0.0.1; User Id = postgres; Password=postgres;Database=sportswebtest;");
            con.Open();
            NpgsqlCommand cmd = new NpgsqlCommand("select * from aspstatetempsessions");
            cmd.Connection = con;

            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var sessionId2 = reader["sessionid"];
                var str = (byte[])reader["sessionitemlong"];

                MemoryStream str3 = new MemoryStream(str);
                var obj2 = (ToCheck1)DeserializeFromStream(str3);
                //DeserializeFromStream(str);
            }

            return new ToCheck1();
        }

        public static void UpdateAndRead()
        {
            NpgsqlConnection con = new NpgsqlConnection("Server = 127.0.0.1; User Id = postgres; Password=postgres;Database=sportswebtest;");
            con.Open();
            NpgsqlCommand cmd = new NpgsqlCommand("\r\n begin;" +
                                                  "\r\n UPDATE ASPStateTempSessions set timeout = 3     WHERE SessionId =  '" + SessionId1 + "';" +
                                                  "\r\n select * from aspstatetempsessions  WHERE SessionId =  '" + SessionId1 + "';"+
                                                  "\r\n commit;");
            cmd.Connection = con;

            var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var sessionId2 = reader["sessionid"];
                var str = (byte[])reader["sessionitemlong"];

                MemoryStream str3 = new MemoryStream(str);
                var obj2 = (ToCheck1)DeserializeFromStream(str3);

            }
        }


        static void InsertRecord(ToCheck1 obj)
        {
            NpgsqlConnection con = new NpgsqlConnection("Server = 127.0.0.1; User Id = postgres; Password=postgres;Database=sportswebtest;");
            con.Open();

            string TempInsertUninitializedItemSql = string.Format(
            "INSERT INTO {0}(SessionId, SessionItemLong, Timeout, Expires, Locked, LockDate, LockDateLocal, LockCookie, Flags)VALUES" +
            "(:{1}, :{2}, :{3}, :{4},0 :: bit, :{5}, :{6},1,1)",
            "ASPStateTempSessions",
            (object)SqlParameterName.SessionId,
            (object)SqlParameterName.SessionItemLong,
            (object)SqlParameterName.Timeout,
            (object)SqlParameterName.Expires,
            (object)SqlParameterName.LockDate,
            (object)SqlParameterName.LockDateLocal);

            var stream = SerializeObject(obj);
            var buff = stream.ToArray();


            NpgsqlCommand cmd = new NpgsqlCommand(TempInsertUninitializedItemSql);
            cmd.Connection = con;
            cmd.Parameters.AddSessionIdParameter(SessionId1)
                .AddSessionItemLongParameter(Convert.ToInt32(stream.Length), buff)
                .AddTimeoutParameter(20)
                .AddLockDateParameter().AddLockDateLocalParameter();

            cmd.ExecuteNonQuery();
        }

        public static MemoryStream SerializeObject(ToCheck1 item)
        {
            MemoryStream stream = new MemoryStream();
            IFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, item);
            return stream;
        }

        public static object DeserializeFromStream(MemoryStream stream)
        {
            IFormatter formatter = new BinaryFormatter();
            stream.Seek(0, SeekOrigin.Begin);
            object o = formatter.Deserialize(stream);
            return o;
        }


        //internal static void SerializeStoreData(
        //    SessionStateStoreData item,
        //    int initialStreamSize,
        //    out byte[] buf,
        //    out int length,
        //    bool compressionEnabled)
        //{
        //    using (MemoryStream s = new MemoryStream(initialStreamSize))
        //    {
        //        Serialize(item, s);
        //        if (compressionEnabled)
        //        {
        //            byte[] serializedBuffer = s.GetBuffer();
        //            int serializedLength = (int)s.Length;
        //            // truncate the MemoryStream so we can write the compressed data in it
        //            s.SetLength(0);
        //            // compress the serialized bytes
        //            using (DeflateStream zipStream = new DeflateStream(s, CompressionMode.Compress, true))
        //            {
        //                zipStream.Write(serializedBuffer, 0, serializedLength);
        //            }
        //            // if the session state tables have ANSI_PADDING disabled, last )s are trimmed.
        //            // This shouldn't happen, but to be sure, we are padding with an extra byte
        //            s.WriteByte((byte)0xff);
        //        }
        //        buf = s.GetBuffer();
        //        length = (int)s.Length;
        //    }
        //}

        //private static void Serialize(SessionStateStoreData item, Stream stream)
        //{
        //    bool hasItems = true;
        //    bool hasStaticObjects = true;

        //    BinaryWriter writer = new BinaryWriter(stream);
        //    writer.Write(item.Timeout);

        //    if (item.Items == null || item.Items.Count == 0)
        //    {
        //        hasItems = false;
        //    }
        //    writer.Write(hasItems);

        //    if (item.StaticObjects == null || item.StaticObjects.NeverAccessed)
        //    {
        //        hasStaticObjects = false;
        //    }
        //    writer.Write(hasStaticObjects);

        //    if (hasItems)
        //    {
        //        ((SessionStateItemCollection)item.Items).Serialize(writer);
        //    }

        //    if (hasStaticObjects)
        //    {
        //        item.StaticObjects.Serialize(writer);
        //    }

        //    // Prevent truncation of the stream
        //    writer.Write(unchecked((byte)0xff));
        //}

        private NpgsqlCommand CreateNpgsqlCommand(string sql)
        {
            NpgsqlCommand NpgsqlCommand = new NpgsqlCommand();
            int num = 1;
            NpgsqlCommand.CommandType = (CommandType)num;
            //int commandTimeout = this._commandTimeout;
            //NpgsqlCommand.CommandTimeout = commandTimeout;
            string str = sql;
            NpgsqlCommand.CommandText = str;
            return NpgsqlCommand;
        }
    }

    internal static class SqlParameterCollectionExtension
    {
        public static NpgsqlParameterCollection AddSessionIdParameter(this NpgsqlParameterCollection pc, string id)
        {
            NpgsqlParameter sqlParameter = new NpgsqlParameter(string.Format("@{0}", (object)SqlParameterName.SessionId),
                NpgsqlDbType.Varchar,
                88);
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
