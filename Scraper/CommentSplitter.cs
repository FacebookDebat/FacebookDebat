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
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
    (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static void SplitWords()
        {
            using (var db = new FacebookDebatEntities())
            {
                log.Info("Finding un-splitted comments");
                var comments = db.Comments.Where(x => !x.splitted).Take(10000).ToList();

                var commentIds = string.Join(",", comments.Select(x => x.id.ToString()).ToArray());
                var deleteWordsTask = Task.Factory.StartNew(() =>
                {
                    if (commentIds.Count() != 0)
                        DatabaseTools.ExecuteNonQuery(string.Format("DELETE FROM CommentWords WHERE comment_id IN ({0})", commentIds));

                    log.Info("Finished deleting words");
                });

                var deleteLinksTask = Task.Factory.StartNew(() =>
                {
                    if (commentIds.Count() != 0)
                        DatabaseTools.ExecuteNonQuery(string.Format("DELETE FROM CommentLinks WHERE comment_id IN ({0})", commentIds));
                    log.Info("Finished deleting links");
                });

                var commentWords = new List<Tuple<int, string>>();
                var commentLinks = new List<Tuple<int, string>>();

                log.Info("Building comment cache");
                var wordCache = new UpdateOrInsertBuilder<Word>(db.Words, x => x.word1, (x, y) => false);

                log.Info("Building link cache");
                var linkCache = new UpdateOrInsertBuilder<Link>(db.Links, x => x.url, (x, y) => false);


                log.Info("Splitting");
                foreach (var comment in comments)
                {
                    // Get links
                    String commentWithoutLinks;
                    var links = Tools.StripLinks(comment.message, out commentWithoutLinks);
                    foreach (var link in links)
                    {
                        linkCache.Process(link, () => new Link() { url = link });
                        commentLinks.Add(Tuple.Create(comment.id, link));
                    }

                    // Get words from link-stripped comment
                    var words = Tools.SplitWords(commentWithoutLinks.ToLower()).Where(x => !string.IsNullOrEmpty(x));
                    foreach (var word in words)
                    {
                        if (word.Length >= 100) // Longest possible word in DB
                        {
                            log.Warn("Ignoring " + word);
                            continue;
                        }

                        if (word.Any(x => char.IsDigit(x) || x == '_')) // no words with underscores or digits
                            continue;

                        if (word.Length < 2)
                            continue;

                        wordCache.Process(word, () => new Word() { word1 = word });

                        commentWords.Add(Tuple.Create(comment.id, word));
                    }
                }

                log.Info("Waiting for delete-tasks");
                Task.WaitAll(deleteLinksTask, deleteWordsTask);

                wordCache.SyncDatabase(2000, "dbo.Words", "id", (word) => new
                {
                    id = (int?)null,
                    word = word.word1
                });

                linkCache.SyncDatabase(2000, "dbo.Links", "id", (link) => new
                {
                    id = (int?)null,
                    url = link.url
                });

                var linkTranslator = new FacebookDebatEntities().Links.ToDictionary(x => x.url, x => x.id);
                DatabaseTools.ChunkInsert("dbo.CommentLinks", 10000, commentLinks.Select(x => new
                {
                    id = (int?)null,
                    comment_id = x.Item1,
                    link_id = linkTranslator[x.Item2]
                }));

                var wordTranslator = new FacebookDebatEntities().Words.ToDictionary(x => x.word1, x => x.id);
                DatabaseTools.ChunkInsert("dbo.CommentWords", 20000, commentWords.Select(x => new
                    {
                        id = (int?)null,
                        comment_id = x.Item1,
                        word_id = wordTranslator[x.Item2]
                    }));

                log.Info("Marking splitted");
                DatabaseTools.ExecuteNonQuery(string.Format("update dbo.Comments set splitted = 1 where id in ({0})", commentIds));
            }
        }

        internal static void StemWords()
        {/*
            using (var db = new FacebookDebatEntities())
            {
                db.Database.CommandTimeout = 0;

                var words = db.Words.Where(x => x.stem_id == null);
                Parallel.ForEach(words, new ParallelOptions { MaxDegreeOfParallelism = 50 }, (word) =>
                {
                    var stemmedWord = LSA.WordCleaner.Clean(word.word1).Single();

                    if (stemmedWord != String.Empty)
                    {
                        try
                        {
                            int stemmedWordId = DatabaseTools.ExecuteFirst("select id from Words where word = @word", (r) => r.GetInt32(0), new SqlParameter("word", stemmedWord));

                            if (stemmedWordId == 0)
                                stemmedWordId = DatabaseTools.ExecuteFirst(@"INSERT INTO Words (word) VALUES (@word);
                                                                         SELECT SCOPE_IDENTITY()",
                                                                                     (r) => (int)r.GetDecimal(0),
                                                                                     new SqlParameter("word", stemmedWord));
                            DatabaseTools.ExecuteNonQuery("update Words set stem_id = @stem_id where word = @word", new SqlParameter("word", word.word1), new SqlParameter("stem_id", stemmedWordId));
                        }
                        catch (Exception e)
                        {

                        }
                    }
                });
            }*/
        }
    }
}
