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
            try
            {
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
            }
            catch(Exception e)
            {
                Console.WriteLine("Error.");
                Console.WriteLine(e.ToString());
;
            }
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
                Parallel.ForEach(scrapees, new ParallelOptions { MaxDegreeOfParallelism = 10}, (scrapee) =>
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
            var scrapedPosts = new List<Post>();
            var updatedComments = new List<Comment>();

            var con_str = ConfigurationManager.ConnectionStrings["FacebookDebat"].ToString();

            Console.WriteLine("Getting comments");
            var newComments = new List<NewComment>();
            var newEntities = new List<Entity>();
            var newEntitiesHashSet = new HashSet<string>();


            using (var db = new FacebookDebatEntities())
            {
                db.Database.CommandTimeout = 0;
                #region Build list of posts to be scraped for comments
                Console.WriteLine("Building post-list");
                var dayLimit = DateTime.Now.AddDays(-lookBackDays);

                // Get new posts, or posts that are less than two days old
                var posts = db.Posts.Where(x => !x.scraped || x.date > dayLimit).ToList();

                // Get posts with comments that has been made less than two days old.
                var postIds = db.Comments.Where(x => x.date > dayLimit).GroupBy(x => x.post_id).Select(x => x.Key);
                posts = posts.Union(db.Posts.Where(x => postIds.Contains(x.id))).Take(500).ToList();
                #endregion

                #region Get Caches
                var ActivePostID = posts.Select(x => x.id).ToList();
                Console.WriteLine("Initializing user-cache");
                var existingEntities = new HashSet<string>(db.Entities.Select(x => x.fb_id));
                Console.WriteLine("Initializing comment-cache");
                var existingComments = db.Comments.Where(x => ActivePostID.Contains(x.post_id)).ToDictionary(x => x.fb_id, x => x);
                #endregion

                int i = 0;

                var fbFailed = false;

                Console.WriteLine("Getting comments from " + posts.Count + " post");
                Parallel.ForEach(posts, (post) =>
                {
                    Interlocked.Increment(ref i);

                    //Console.WriteLine("Post " + i++ + "/" + posts.Count);

                    if (fbFailed)
                        return;

                    List<Facebook.Comment> fb_comments;
                    try
                    {
                        fb_comments = fb.GetComments(post.fb_id);
                    }
                    catch (Exception e)
                    {
                        fbFailed = true;
                        Console.WriteLine("Being ignored by Facebook. Aborting.");
                        Console.WriteLine(e.Message);

                        return;
                    }



                    foreach (var fb_comment in fb_comments)
                    {
                        if (!existingEntities.Contains(fb_comment.user_id) && !newEntitiesHashSet.Contains(fb_comment.user_id))
                        {
                            lock (newEntities)
                            {
                                newEntities.Add(new Entity()
                                {
                                    name = fb_comment.user_name,
                                    fb_id = fb_comment.user_id
                                });
                                newEntitiesHashSet.Add(fb_comment.user_id);
                            }
                        }
                        Comment comment;
                        if (existingComments.TryGetValue(fb_comment.id, out comment))
                        {
                            if (fb_comment.message != comment.message)
                            {
                                lock (updatedComments)
                                    updatedComments.Add(comment);

                                comment.message = fb_comment.message;
                                comment.score = Classifier.Classify(fb_comment.message);
                            }
                        }
                        else
                        {
                            lock (newComments)
                                newComments.Add(new NewComment()
                                {
                                    fb_id = fb_comment.id,
                                    message = fb_comment.message,
                                    post_id = post.id,
                                    user_fb_id = fb_comment.user_id,
                                    date = fb_comment.date,
                                    score = Common.Classifier.Classify(fb_comment.message)
                                });
                        }
                    }
                    lock (scrapedPosts)
                        scrapedPosts.Add(post);
                });

                Console.WriteLine("Updating " + updatedComments.Count() + " comments");
                Parallel.ForEach(updatedComments, (comment) =>
                {
                    DatabaseTools.ExecuteNonQuery("UPDATE dbo.[Comments] SET message = @comment WHERE id = @id", new SqlParameter("id", comment.id), new SqlParameter("comment", comment.message));
                });
            }

            // Add all new users
            Console.WriteLine("Inserting " + newEntities.Count + " entities");
            var entityInsertedCount = 0;
            var entityBulkSize = int.Parse(ConfigurationManager.AppSettings["UserBulkSize"]);
            foreach (var entityList in newEntities.Chunk(entityBulkSize))
            {
                DatabaseTools.BulkInsert("dbo.Entities", entityList.Select(x => new
                {
                    id = (int?)null,
                    fb_id = x.fb_id,
                    name = x.name
                }));

                entityInsertedCount += entityList.Count();
                Console.WriteLine("Inserted " + entityInsertedCount + "/" + newEntities.Count + " new entities");
            }

            var commentInsertedCount = 0;
            var idTranslator = new FacebookDebatEntities().Entities.ToDictionary(x => x.fb_id, x => x.id);
            var commentBulkSize = int.Parse(ConfigurationManager.AppSettings["CommentBulkSize"]);
            foreach (var comments in newComments.Chunk(commentBulkSize))
            {
                DatabaseTools.BulkInsert("dbo.[Comments]",
                    comments.Select(x => new
                    {
                        id = (int?)null,
                        fb_id = x.fb_id,
                        post_id = x.post_id,
                        user_id = idTranslator[x.user_fb_id],
                        date = x.date,
                        score = x.score,
                        scored = 1,
                        message = x.message,
                        splitted = 0,
                    }));
                commentInsertedCount += comments.Count();
                Console.WriteLine("Inserted " + commentInsertedCount + "/" + newComments.Count + " new comments");
            }

            Console.WriteLine("Marking scraped");
            Parallel.ForEach(scrapedPosts, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, scrapedPost =>
            {
                DatabaseTools.ExecuteNonQuery("UPDATE dbo.[Posts] SET scraped = 1 WHERE id = @id", new SqlParameter("id", scrapedPost.id));
            });
        }
    }
}
