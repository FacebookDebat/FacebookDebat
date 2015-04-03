using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    public class DatabaseTools
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [DebuggerStepThroughAttribute]
        public static List<T> ExecuteReader<T>(string s, Func<SqlDataReader, T> f, params SqlParameter[] parms)
        {
            return Do(s, parms, () =>
            {
                var rv = new List<T>();
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString))
                {
                    conn.Open();
                    var command = conn.CreateCommand();
                    command.CommandText = s;
                    command.Parameters.AddRange(parms);
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        rv.Add(f(reader));
                    }
                    return rv;
                }
            });
        }
        public static Task<List<Dictionary<string, object>>> ExecuteReaderAsync(string s, params SqlParameter[] parms)
        {
            var t = new Task<List<Dictionary<string, object>>>(() => ExecuteReader(s, parms));
            t.Start();
            return t;
        }

        [DebuggerStepThroughAttribute]
        public static T ExecuteSingle<T>(string s, Func<SqlDataReader, T> r, params SqlParameter[] parms)
        {
            return Do(s, parms, () =>
            {
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString))
                {
                    conn.Open();
                    var command = conn.CreateCommand();
                    command.CommandText = s;
                    command.Parameters.AddRange(parms);
                    var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        return r(reader);
                    }
                }
                throw new Exception("...");
            });
        }

        [DebuggerStepThroughAttribute]
        public static T ExecuteFirst<T>(string s, Func<SqlDataReader, T> r, params SqlParameter[] parms)
        {
            return Do(s, parms, () =>
            {
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString))
                {
                    conn.Open();
                    var command = conn.CreateCommand();
                    command.CommandText = s;
                    command.Parameters.AddRange(parms);
                    var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        return r(reader);
                    }
                }
                return default(T);
            });
        }

        [DebuggerStepThroughAttribute]
        public static List<Dictionary<string, object>> ExecuteReader(string s, params SqlParameter[] parms)
        {
            return Do(s, parms, () =>
            {

                var rv = new List<Dictionary<string, object>>();
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString))
                {
                    conn.Open();
                    var command = conn.CreateCommand();
                    command.CommandText = s;
                    command.CommandTimeout = 0;
                    command.Parameters.AddRange(parms);
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var obj = new Dictionary<string, object>();
                        for (int field = 0; field < reader.FieldCount; field++)
                        {
                            obj[reader.GetName(field)] = reader.GetValue(field);
                        }
                        rv.Add(obj);
                    }
                    return rv;
                }
            });
        }
        public static void ExecuteNonQuery(string s, params SqlParameter[] parms)
        {
            Do(s, parms, () =>
            {
                var rv = new List<Dictionary<string, object>>();
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString))
                {
                    conn.Open();
                    var command = conn.CreateCommand();
                    command.CommandTimeout = 0;
                    command.CommandText = s;
                    command.Parameters.AddRange(parms);
                    var reader = command.ExecuteNonQuery();
                }
                return 1;
            });
        }

        // http://www.c-sharpcorner.com/Forums/Thread/31044/how-to-convert-system-type-to-dbtype.aspx
        public static DbType TypeToDbType(Type t)
        {
            DbType dbt;
            try
            {
                dbt = (DbType)Enum.Parse(typeof(DbType), t.Name);
            }
            catch
            {
                dbt = DbType.Object;
            }
            return dbt;
        }

        [DebuggerStepThroughAttribute]
        public static void Update<T>(string table, string idColumn, IEnumerable<T> items)
        {
            var itemColumns = typeof(T).GetProperties().ToList();
            var columnClauses = itemColumns.Where(x => x.Name != idColumn).Select(x => x.Name + " = @" + x.Name);

            var sql = "UPDATE " + table + " SET " + String.Join(",", columnClauses) + " where " + idColumn + " = @" + idColumn;

            Parallel.ForEach(items, new ParallelOptions { MaxDegreeOfParallelism = 1 }, item =>
            {
                var paramArray = itemColumns.Select(column => new SqlParameter(column.Name, TypeToDbType(Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType)) { Value = (object)column.GetValue(item) ?? DBNull.Value });
                DatabaseTools.ExecuteNonQuery(sql, paramArray.ToArray());
            });
        }



        public static void ChunkInsert<T>(string table, int size, IEnumerable<T> items)
        {
            var cnt = items.Count();
            int i = 0;

            log.Info("ChunkInserting " + cnt + " items to " + table);
            var chunks = items.Chunk(size);
            foreach (var chunk in chunks)
            {
                BulkInsert(table, chunk);
                i += chunk.Count();
                log.Info("Inserted " + i + "/" + cnt + " items");
            }
        }
        [DebuggerStepThroughAttribute]
        public static void BulkInsert<T>(string table, IEnumerable<T> items)
        {
            var columns = typeof(T).GetProperties().ToList();
            var InsertTableTable = new DataTable(table);
            foreach (var column in columns)
            {
                var type = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;
                InsertTableTable.Columns.Add(column.Name, type);
            }

            foreach (var item in items)
            {
                var values = new object[columns.Count];
                for (int i = 0; i < columns.Count; i++)
                {
                    values[i] = columns[i].GetValue(item);
                }
                InsertTableTable.Rows.Add(values);
            }
            Do(() =>
            {
                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString, SqlBulkCopyOptions.KeepIdentity & SqlBulkCopyOptions.KeepNulls))
                {
                    bulkCopy.DestinationTableName = table;
                    bulkCopy.WriteToServer(InsertTableTable);
                }
                return 1;
            });

        }

        public static Task<List<T>> ExecuteListReaderAsync<T>(string query, Func<SqlDataReader, T> getValues, params SqlParameter[] parms)
        {
            var t = new Task<List<T>>(() => ExecuteListReader(query, getValues, parms));
            t.Start();
            return t;
        }

        public static List<T> ExecuteListReader<T>(string query, Func<SqlDataReader, T> getValues, params SqlParameter[] parms)
        {
            return Do(query, parms, () =>
            {
                var list = new List<T>();
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString))
                {
                    conn.Open();
                    var command = conn.CreateCommand();
                    command.CommandTimeout = 0;
                    command.CommandText = query;
                    command.Parameters.AddRange(parms);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(getValues(reader));
                        }
                        reader.Close();

                    }
                }
                return list;
            });

        }

        public static Dictionary<T, Y> ExecuteDictionaryReader<T, Y>(string query, Func<SqlDataReader, T> getKey, Func<SqlDataReader, Y> getValue, params SqlParameter[] parms)
        {
            return Do(query, parms, () =>
            {
                var dict = new Dictionary<T, Y>();
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString))
                {
                    conn.Open();
                    var command = conn.CreateCommand();
                    command.CommandTimeout = 0;
                    command.CommandText = query;
                    command.Parameters.AddRange(parms);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dict.Add(getKey(reader), getValue(reader));
                        }
                        reader.Close();

                    }
                }

                return dict;
            });
        }
        class SqlEception: Exception 
        {
            public readonly string query;
            public readonly SqlParameter[] parms;
            public SqlEception(string query, SqlParameter[] parms, Exception innerexception) : base("SQL Problems", innerexception)
            {
                this.query = query;
                this.parms = parms;
            }

            public override string ToString()
            {
                var sql = String.Join(";\r\n", parms.Select(x => x.ParameterName + " = " + x.Value.ToString()).ToArray()) + "\r\n"
                    + query + "\r\n";

                return sql + base.ToString();
            }
        }
        public static T Do<T>(string query, SqlParameter[] parms, Func<T> action, int? times = null, int wait = 1000, Action<Exception> onError = null)
        {
            if (times == null)
                times = 20;

            for (int i = 0; i < times; i++)
            {
                try
                {
                    return action();
                }
                catch (Exception e)
                {
                    if (onError != null)
                        onError(e);

                    if (i == times - 1)
                        throw new SqlEception(query, parms, e);

                    Thread.Sleep(wait);

                }
            }
            throw new NotImplementedException("Should never happen");
        }


        public static T Do<T>(Func<T> action, int? times = null, int wait = 1000, Action<Exception> onError = null)
        {
            if (times == null)
                times = 20;

            for (int i = 0; i < times; i++)
            {
                try
                {
                    return action();
                }
                catch (Exception e)
                {
                    if (onError != null)
                        onError(e);

                    Thread.Sleep(wait);
                    if (i == times - 1)
                        throw;

                }
            }
            throw new NotImplementedException("Should never happen");
        }
    }
}
