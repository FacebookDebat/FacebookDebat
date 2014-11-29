﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common
{
    public static class Tools
    {

        public static List<T> ExecuteReader<T>(string s, Func<SqlDataReader, T> f, params SqlParameter[] parms)
        {
            var rv = new List<T>();
            SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString);
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

        //http://mukundsideas.blogspot.dk/2010/07/how-to-split-sentence-into-word-using-c.html
        public static string[] SplitWords(string s)
        {
            //
            // Split on all non-word characters.
            // ... Returns an array of all the words.
            //
            return Regex.Split(s, @"\W+");
            // @      special verbatim string syntax
            // \W+    one or more non-word characters together
        }

        // http://stackoverflow.com/questions/419019/split-list-into-sublists-with-linq
        /// <summary>
        /// Break a list of items into chunks of a specific size
        /// </summary>
        public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int chunksize)
        {
            while (source.Any())
            {
                yield return source.Take(chunksize);
                source = source.Skip(chunksize);
            }
        }
    }
}
