using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Configuration;
using Common.Data;
using Common;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Threading;
using System.Globalization;

namespace Scraper
{
    class Program
    {
        struct NewComment
        {
            public string fb_id;
            public string message;
            public int post_id;
            public string user_fb_id;
            public DateTime date;
            public double? score;
        }

        static void Main(string[] args)
        {
#if !DEBUG
            try
            {
#endif
                var fb = new Facebook();

                int lookbackDays = int.Parse(ConfigurationManager.AppSettings["LookbackDays"]);
                bool getNames = bool.Parse(ConfigurationManager.AppSettings["GetNames"]);
                bool getPosts = bool.Parse(ConfigurationManager.AppSettings["GetPosts"]);
                bool getComments = bool.Parse(ConfigurationManager.AppSettings["GetComments"]);
                bool splitComments = bool.Parse(ConfigurationManager.AppSettings["SplitComments"]);
                bool stemWords = bool.Parse(ConfigurationManager.AppSettings["StemWords"]);

                if (getNames)
                    GetScrapees(fb);
                if (getPosts)
                {
                    var date = DateTime.Now;
                    if (args.Length == 1)
                        date = DateTime.ParseExact(args[0], "yyyyMMdd", CultureInfo.InvariantCulture);
                    GetPosts(fb, lookbackDays, date);
                }
                if (getComments)
                    GetComments(fb, lookbackDays);
                if (splitComments)
                    CommentSplitter.SplitWords();
                if (stemWords)
                    CommentSplitter.StemWords();

                using (var db = new FacebookDebatEntities())
                {
                    var unclassifiedComments = db.Comments.Where(x => !x.scored).ToList();
                    if (unclassifiedComments.Count > 0)
                    {
                        Console.WriteLine("Reclassifying " + unclassifiedComments.Count + " comments");
                        Parallel.ForEach(unclassifiedComments, (comment) =>
                        {
                            DatabaseTools.ExecuteNonQuery("update dbo.comments set scored = 1, score = @score where id = @id",
                                new SqlParameter("score", Classifier.Classify(comment.message)),
                                new SqlParameter("id", comment.id));
                        });
                    }
                }

                Console.WriteLine("Finished.");

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.ReadLine();
                }
#if !DEBUG
            }
            catch (Exception e)
            {
                Console.WriteLine("Error.");
                Console.WriteLine(e.ToString());
                ;
            }
#endif
        }

        private static void GetScrapees(Facebook fb)
        {
            Console.WriteLine("Verifying scrapees");
            using (var db = new FacebookDebatEntities())
            {
                var scrapees = db.Scrapees.Where(x => x.entity_id == null && x.enabled).ToList();
                Console.WriteLine("Found " + scrapees.Count + " new entities.");
                foreach (var scrapee in scrapees)
                {
                    try
                    {
                        var fb_entity = fb.GetUser(scrapee.fb_id);

                        var entity = new Entity
                        {
                            fb_id = fb_entity.fb_id,
                            name = fb_entity.name
                        };

                        db.Entities.Add(entity);

                        scrapee.Entity = entity;
                        scrapee.name = fb_entity.name;

                        db.Entry(scrapee).State = System.Data.Entity.EntityState.Modified;
                    }
                    catch (Exception e) { }
                }
                Console.WriteLine("Done");
                db.SaveChanges();
            }
        }

        private static void GetPosts(Facebook fb, int lookbackDays, DateTime dateFrom)
        {
            using (var db = new FacebookDebatEntities())
            {
                db.Configuration.AutoDetectChangesEnabled = false;
                var scrapees = db.Scrapees.Where(x => x.enabled).Select(x => new { name = x.name, entity = x.Entity }).ToList();

                // Get posts for each page
                Parallel.ForEach(scrapees, new ParallelOptions { MaxDegreeOfParallelism = 10 }, (scrapee) =>
                {
                    Console.WriteLine("Getting posts for " + scrapee.name);

                    if (scrapee.entity == null)
                        return;

                    try
                    {
                        var fb_posts = fb.GetPosts(scrapee.entity.fb_id, lookbackDays, dateFrom);
                        foreach (var fb_post in fb_posts)
                        {
                            Post post;
                            lock (db)
                                post = db.Posts.SingleOrDefault(x => x.fb_id == fb_post.id);

                            if (post == null)
                            {
                                post = new Post()
                                {
                                    fb_id = fb_post.id,
                                    message = fb_post.message,
                                    Entity = scrapee.entity,
                                    date = fb_post.date
                                };
                                lock (db)
                                    db.Posts.Add(post);
                            }
                            else
                                lock (db)
                                    post.message = fb_post.message;
                        }
                        lock (db)
                            db.SaveChanges();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Problem with " + scrapee.name);
                        Console.WriteLine(e.ToString());
                    }
                });
            }
        }

        private static void GetComments(Facebook fb, int lookBackDays)
        {
            Console.WriteLine("Getting comments");
            using (var db = new FacebookDebatEntities())
            {
                var scrapedPosts = new List<Post>();

                db.Database.CommandTimeout = 0;

                #region Build list of posts to be scraped for comments
                Console.WriteLine("Building post-list");
                var dayLimit = DateTime.Now.AddDays(-lookBackDays);

                // Get new posts, or posts that are less than two days old
                var posts = db.Posts.Where(x => !x.scraped || x.date > dayLimit).ToList();

                // Get posts with comments that has been made less than two days old.
                var latestedCommentedPosts = db.Comments.Where(x => x.date > dayLimit).GroupBy(x => x.post_id).Select(x => x.Key);
                posts = posts.Union(db.Posts.Where(x => latestedCommentedPosts.Contains(x.id))).ToList();
                #endregion

                #region Scrape Comments from FB
                List<List<Facebook.Comment>> postComments = new List<List<Facebook.Comment>>();
                Console.WriteLine("Getting comments from " + posts.Count + " post");
                var fbFailed = false;
                Parallel.ForEach(posts, (post) =>
                {
                    if (fbFailed)
                        return;

                    try
                    {
                        var comments = fb.GetComments(post.fb_id);
                        lock (postComments) { 
                            postComments.Add(comments);
                            scrapedPosts.Add(post);
                        }

                    }
                    catch (Exception e)
                    {
                        fbFailed = true;
                        Console.WriteLine("Being ignored by Facebook. Aborting.");
                        Console.WriteLine(e.Message);

                        return;
                    }
                });
                #endregion

                #region Update Entities
                Console.WriteLine("Processing entities");
                var entityUpdater = new UpdateOrInsertBuilder<Entity>(
                    AlreadyExists: db.Entities.Select(x => x),
                    GetKey: x => x.fb_id,
                    Updater: (dbItem, memItem) => {
                        if (dbItem.name != memItem.name)
                        {
                            dbItem.name = memItem.name;
                            return true;
                        }
                        return false;
                    }
                );

                foreach (var post in postComments)
                {
                    foreach (var comment in post)
                    {
                        entityUpdater.Process(comment.user_id, () => new Entity
                        {
                            fb_id = comment.user_id,
                            name = comment.user_name
                        });
                    }
                }

                entityUpdater.SyncDatabase(2000, "dbo.Entities", "id", x => new {
                                                id = (int?)null,
                                                fb_id = x.fb_id,
                                                name = x.name
                                            });
                #endregion

                #region Update comments
                Console.WriteLine("Initializing comment-cache");
                var ActivePostID = posts.Select(x => x.id).ToList();

                var commentUpdater = new UpdateOrInsertBuilder<Comment>(
                                    AlreadyExists: db.Comments.Where(x => ActivePostID.Contains(x.post_id)),
                                    GetKey: x => x.fb_id,
                                    Updater: (dbItem, memItem) => {
                                        if (dbItem.message != memItem.message)
                                        {
                                            dbItem.message = memItem.message;
                                            return true;
                                        }
                                        return false;
                                    }
                                );

                var postTranslator = db.Posts.ToDictionary(x => x.fb_id, x => x.id);
                var entityTranslator = db.Entities.ToDictionary(x => x.fb_id, x => x.id);
                Console.WriteLine("Processing comments");
                foreach (var post in postComments)
                {
                    foreach (var comment in post)
                    {
                        commentUpdater.Process(comment.id, () => new Comment()
                            {
                                fb_id = comment.id,
                                message = comment.message,
                                post_id = postTranslator[comment.post_id],
                                entity_id = entityTranslator[comment.user_id],
                                date = comment.date,
                                score = Common.Classifier.Classify(comment.message)
                            });

                    }
                }
                commentUpdater.SyncDatabase(2000, "dbo.[Comments]", "id", x => new
                        {
                            id = (int?)null,
                            fb_id = x.fb_id,
                            post_id = x.post_id,
                            entity_id = x.entity_id,
                            date = x.date,
                            score = x.score,
                            scored = 1,
                            message = x.message,
                            splitted = 0,
                        });
                #endregion

                Console.WriteLine("Marking scraped");
                Parallel.ForEach(scrapedPosts, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, scrapedPost =>
                {
                    if(!scrapedPost.scraped)
                        DatabaseTools.ExecuteNonQuery("UPDATE dbo.[Posts] SET scraped = 1 WHERE id = @id", new SqlParameter("id", scrapedPost.id));
                });
            }
        }
    }
}
