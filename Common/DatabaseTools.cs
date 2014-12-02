using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class DatabaseTools
    {
        [DebuggerStepThroughAttribute]
        public static List<T> ExecuteReader<T>(string s, Func<SqlDataReader, T> f, params SqlParameter[] parms)
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
        }

        [DebuggerStepThroughAttribute]
        public static List<Dictionary<string, object>> ExecuteReader(string s, params SqlParameter[] parms)
        {
            var rv = new List<Dictionary<string, object>>();
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString))
            {
                conn.Open();
                var command = conn.CreateCommand();
                command.CommandText = s;
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
        }
        [DebuggerStepThroughAttribute]
        public static void ExecuteNonQuery(string s, params SqlParameter[] parms)
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

            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString, SqlBulkCopyOptions.KeepIdentity & SqlBulkCopyOptions.KeepNulls))
            {
                bulkCopy.DestinationTableName = table;
                bulkCopy.WriteToServer(InsertTableTable);
            }

        }
    }
}
