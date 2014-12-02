using Common;
using Common.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Scraper
{
    class CommentSplitter
    {
        public static void SplitWords()
        {
            Console.WriteLine("Splitting comments");
            var newWords = new HashSet<string>();
            var commentWords = new List<Tuple<int, string>>();

            List<int> commentIDList;

            using (var db = new FacebookDebatEntities())
            {
                Console.WriteLine("Initalizing word-list");
                var seenWords = new HashSet<string>(db.Words.Select(x => x.word1));

                db.Configuration.AutoDetectChangesEnabled = false;
                        
                Console.WriteLine("Finding un-splitted comments");
                var comments = db.Comments.Where(x => !x.splitted).Take(10000).ToList();
                commentIDList = comments.Select(x => x.id).ToList();

                Console.WriteLine("Splitting");
                int i = 0;
                foreach (var comment in comments)
                {
                    if (i % 100 == 0)
                        Console.WriteLine("Splitting comment " + i + "/" + comments.Count());
                    i++;

                    var words = Tools.SplitWords(comment.message.ToLower()).Where(x => !string.IsNullOrEmpty(x));
                    foreach (var word in words)
                    {
                        if (word.Length >= 100)
                        {
                            Console.WriteLine("Ignoring " + word);
                            continue;
                        }

                        if (word.Any(x => char.IsDigit(x) || x == '_') || word.Length < 2)
                            continue;

                        if (!seenWords.Contains(word))
                        {
                            newWords.Add(word);
                            seenWords.Add(word);
                        }

                        commentWords.Add(Tuple.Create(comment.id, word));
                    }
                }
            }
            var connectionString = ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString;

            // Delete old words.
            Console.WriteLine("Cleaning up");
            Parallel.ForEach(commentIDList, new ParallelOptions { MaxDegreeOfParallelism = 10 }, (commentId) =>
            {
                DatabaseTools.ExecuteNonQuery("DELETE FROM CommentWords WHERE comment_id = @id", new SqlParameter("id", commentId));
            });

            Console.WriteLine("Inserting " + newWords.Count + " new words");
            DatabaseTools.BulkInsert("dbo.Words",
                newWords.Select(x =>
                    new
                    {
                        id = (int?)null,
                        word = x
                    }));

            // Add all commentWords
            Console.WriteLine("Inserting " + commentWords.Count + " new comment-words");
            int cnt = 0;
            var commentWordChunks = commentWords.Chunk(10000);
            var idTranslator = new FacebookDebatEntities().Words.ToDictionary(x => x.word1, x => x.id);
            foreach(var chunk in commentWordChunks)
            {
                DatabaseTools.BulkInsert("dbo.CommentWords", chunk.Select(x => new
                {
                    id = (int?)null,
                    comment_id = x.Item1,
                    word_id = idTranslator[x.Item2]
                }));
                Interlocked.Add(ref cnt, chunk.Count());
                Console.WriteLine("Inserted " + cnt + "/" + commentWords.Count + " commentwords");
            }

            Console.WriteLine("Marking splitted");
            Parallel.ForEach(commentIDList, new ParallelOptions {  MaxDegreeOfParallelism = 10 }, (id) =>
            {
                DatabaseTools.ExecuteNonQuery("update dbo.Comments set splitted = 1 where id = @id", new SqlParameter("id", id));
            });
            Console.WriteLine("Finisihed.");
        }
    }
}
