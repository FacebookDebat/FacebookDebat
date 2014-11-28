using Common.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EntityFramework.BulkInsert.Extensions;
using System.Data.SqlClient;
using System.Data;
using System.Data.Entity;
using Common;

namespace Classifier
{
    public class Program
    {
        static void Main(string[] args)
        {
            ClassifyWithSentimentList();
        }

        static void ClassifyWithSentimentList()
        {
            var batchSize = 50;
            var cnt = 0;
            var commentCount = new FacebookDebatEntities().Comments.Count(x => !x.scored);
            while (true)
            {
                using (var db = new FacebookDebatEntities())
                {
                    db.Configuration.AutoDetectChangesEnabled = false;
                    var comments = db.Comments.Where(x => !x.scored).OrderBy(x => x.id).Take(batchSize).ToList();
                    if (comments.Count() == 0)
                        break;
                    foreach (var comment in comments)
                    {
                        comment.score = Common.Classifier.Classify(comment.message);
                        comment.scored = true;
                        db.Entry(comment).State = EntityState.Modified;
                    }
                    Console.WriteLine("Classified comment " + (cnt * batchSize) + "/" + commentCount);
                    cnt++;
                    db.SaveChanges();
                }
            }
        }

        static void SplitWords()
        {
            string connectionString;

            var newWords = new List<string>();
            var commentWords = new List<Tuple<int, string>>();

            using (var db = new FacebookDebatEntities())
            {
                var seenWords = db.Words.ToDictionary(x => x.word1, x => x.word1);

                db.Configuration.AutoDetectChangesEnabled = false;

                var comments = db.Comments.ToList();

                int i = 0;
                foreach (var comment in comments)
                {
                    if (i % 2000 == 0)
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

                        string consistentWordInstance;
                        if (!seenWords.TryGetValue(word, out consistentWordInstance))
                        {
                            newWords.Add(word);
                            seenWords.Add(word, word);
                            consistentWordInstance = word;
                        }

                        if (consistentWordInstance == null)
                            throw new Exception();

                        commentWords.Add(Tuple.Create(comment.id, consistentWordInstance));
                    }
                }
                connectionString = db.Database.Connection.ConnectionString;
            }

            // We have now gathered all words
            var InsertWordTable = new DataTable("NewWords");
            InsertWordTable.Columns.Add("id", typeof(int));
            InsertWordTable.Columns.Add("word", typeof(string));

            foreach (var word in newWords)
                InsertWordTable.Rows.Add(null, word);

            Console.WriteLine("Inserting " + newWords.Count + " new words");
            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connectionString, SqlBulkCopyOptions.KeepIdentity & SqlBulkCopyOptions.KeepNulls))
            {
                bulkCopy.DestinationTableName = "dbo.Words";
                bulkCopy.WriteToServer(InsertWordTable);
            }

            // Add all commentWords
            var InsertCommentWordTable = new DataTable("NewCommentWords");
            InsertCommentWordTable.Columns.Add("id", typeof(int));
            InsertCommentWordTable.Columns.Add("comment_id", typeof(int));
            InsertCommentWordTable.Columns.Add("word_id", typeof(int));

            var idTranslator = new FacebookDebatEntities().Words.ToDictionary(x => x.word1, x => x.id);
            foreach (var commentWord in commentWords)
                InsertCommentWordTable.Rows.Add(null, commentWord.Item1, idTranslator[commentWord.Item2]);

            Console.WriteLine("Inserting " + commentWords.Count + " CommentWords");
            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connectionString, SqlBulkCopyOptions.KeepIdentity & SqlBulkCopyOptions.KeepNulls))
            {
                bulkCopy.DestinationTableName = "dbo.CommentWords";
                bulkCopy.WriteToServer(InsertCommentWordTable);
            }

            Console.WriteLine("Finsihed.");
        }

    }
}
