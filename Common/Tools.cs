
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common
{
    public static class Tools
    {
        public static long ToUnixTimestamp(this DateTime t)
        {
            return (long)(t.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
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


        static Regex url = new System.Text.RegularExpressions.Regex(@"((http|ftp|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?)", RegexOptions.Multiline);


        public static List<string> StripLinks(string str, out string commentWithoutLinks)
        {
            var matches = url.Matches(str);

            var newComment = url.Replace(str, "");
            commentWithoutLinks = newComment;

            return matches.Cast<Match>().Select(x => x.ToString()).ToList();
        }


    }
}
