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
[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Scraper
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static void Main(string[] args)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Start(args);
            }
            else
            {
                try
                {
                    Start(args);
                }
                catch (Exception e)
                {
                    log.Error("Unhandled", e);
                }
            }

            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.ReadLine();
            }
        }

        private static void Start(string[] args)
        {
            var fb = new Facebook();

            int lookbackDays = int.Parse(ConfigurationManager.AppSettings["LookbackDays"]);
            int maxUnscrapedPosts = int.Parse(ConfigurationManager.AppSettings["MaxUnscrapedPosts"]);
            bool getNames = bool.Parse(ConfigurationManager.AppSettings["GetNames"]);
            bool getPosts = bool.Parse(ConfigurationManager.AppSettings["GetPosts"]);
            bool getComments = bool.Parse(ConfigurationManager.AppSettings["GetComments"]);
            bool dailyScraping = bool.Parse(ConfigurationManager.AppSettings["DailyScraping"]);
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
                ScrapePosts(fb, lookbackDays, maxUnscrapedPosts, dailyScraping);
            if (splitComments)
                CommentSplitter.SplitWords();
            if (stemWords)
                CommentSplitter.StemWords();

            using (var db = new FacebookDebatEntities())
            {
                var unclassifiedComments = db.Comments.Where(x => !x.scored).ToList();
                if (unclassifiedComments.Count > 0)
                {
                    log.Info("Reclassifying " + unclassifiedComments.Count + " comments");
                    Parallel.ForEach(unclassifiedComments, (comment) =>
                    {
                        DatabaseTools.ExecuteNonQuery("update dbo.comments set scored = 1, score = @score where id = @id",
                            new SqlParameter("score", Classifier.Classify(comment.message)),
                            new SqlParameter("id", comment.id));
                    });
                }
            }

            log.Info("Finished.");
        }

        private static void GetScrapees(Facebook fb)
        {
            log.Info("Verifying scrapees");
            using (var db = new FacebookDebatEntities())
            {
                var scrapees = db.Scrapees.Where(x => x.entity_id == null && x.enabled).ToList();
                log.Info("Found " + scrapees.Count + " new entities.");
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
                    catch (Exception) { }
                }
                log.Info("Done");
                db.SaveChanges();
            }
        }

        private static void GetPosts(Facebook fb, int lookbackDays, DateTime dateFrom)
        {
            using (var db = new FacebookDebatEntities())
            {
                db.Configuration.AutoDetectChangesEnabled = false;
                var scrapees = db.Scrapees.Where(x => x.enabled).Select(x => new { name = x.name, entity = x.Entity }).ToList();

                log.Info("Getting posts for " + scrapees.Count + " scrapees.");
                // Get posts for each page
                Parallel.ForEach(scrapees, new ParallelOptions { MaxDegreeOfParallelism = 15 }, (scrapee) =>
                {

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
                                    date = fb_post.date,
                                    shares = fb_post.shares
                                };
                                lock (db)
                                    db.Posts.Add(post);
                            }
                            else
                                lock (db)
                                {
                                    if (post.message != fb_post.message || post.shares != fb_post.shares)
                                    {
                                        post.message = fb_post.message;
                                        post.shares = fb_post.shares;
                                        db.Entry(post).State = System.Data.Entity.EntityState.Modified;
                                    }
                                }
                        }
                        lock (db)
                            db.SaveChanges();
                    }
                    catch (Exception e)
                    {
                        log.Error("Problem with " + scrapee.name);
                        log.Error(e.ToString());
                    }
                });
                log.Info("Done.");
            }
        }

        private static void ScrapePosts(Facebook fb, int lookBackDays, int maxUnscrapedPosts, bool dailyScraping)
        {
            log.Info("Scraping posts");
            using (var db = new FacebookDebatEntities())
            {

                db.Database.CommandTimeout = 0;

                #region Build list of posts to be scraped for comments
                log.Info("Building post-list");
                var dayLimit = DateTime.Now.AddDays(-lookBackDays);

                // Get unscraped posts
                var posts = db.Posts.Where(x => !x.scraped).OrderBy(x => x.date).Take(maxUnscrapedPosts).ToList();

                // Get posts with comments that has been made less than two days old
                if (dailyScraping)
                {
                    posts = posts.Union(db.Posts.Where(x => x.date > dayLimit)).ToList();
                    var latestedCommentedPosts = db.Comments.Where(x => x.date > dayLimit).GroupBy(x => x.post_id).Select(x => x.Key);
                    posts = posts.Union(db.Posts.Where(x => latestedCommentedPosts.Contains(x.id))).ToList();
                }
                #endregion

                log.Info("Found " + posts.Count + " posts");

                foreach (var postChunk in posts.Chunk(130))
                {
                    var scrapedPosts = new List<Post>();

                    #region Scrape Comments from FB
                    List<Facebook.Comment> postComments = new List<Facebook.Comment>();
                    List<Facebook.PostLike> postLikes = new List<Facebook.PostLike>();
                    log.Info("Getting comments and likes from " + postChunk.Count() + " post");
                    var fbFailed = false;
                    Parallel.ForEach(postChunk, (post) =>
                    {
                        if (fbFailed)
                            return;

                        try
                        {
                            var comments = Task.Run(() => fb.GetComments(post.fb_id));
                            var likes = Task.Run(() => fb.GetLikes(post.fb_id));
                            comments.Wait();
                            likes.Wait();
                            lock (postComments)
                            {
                                postComments.AddRange(comments.Result);
                                postLikes.AddRange(likes.Result);
                                scrapedPosts.Add(post);
                            }
                        }
                        catch (Exception e)
                        {
                            //fbFailed = true;
                            log.Warn("Being ignored by Facebook. Aborting.");
                            log.Warn(e.Message);

                            return;
                        }
                    });
                    #endregion

                    #region Update Entities
                    var entityUpdater = new UpdateOrInsertBuilder<Entity>(
                        AlreadyExists: db.Entities,
                        GetKey: x => x.fb_id,
                        Updater: (dbItem, memItem) =>
                        {
                            if (dbItem.name != memItem.name)
                            {
                                dbItem.name = memItem.name;
                                return true;
                            }
                            return false;
                        }
                    );

                    foreach (var comment in postComments)
                    {
                        entityUpdater.Process(comment.user_id, () => new Entity
                        {
                            fb_id = comment.user_id,
                            name = comment.user_name
                        });
                    }
                    foreach (var like in postLikes)
                    {
                        entityUpdater.Process(like.user_id, () => new Entity
                        {
                            fb_id = like.user_id,
                            name = like.user_name
                        });
                    }
                    entityUpdater.SyncDatabase(2000, "dbo.Entities", "id", x => new
                    {
                        id = (int?)null,
                        fb_id = x.fb_id,
                        name = x.name
                    });
                    #endregion

                    var postTranslator = db.Posts.ToDictionary(x => x.fb_id, x => x.id);
                    var entityTranslator = db.Entities.ToDictionary(x => x.fb_id, x => x.id);
                    var ActivePostID = postChunk.Select(x => x.id).ToList();

                    #region Update comments
                    var commentUpdater = new UpdateOrInsertBuilder<Comment>(
                                        AlreadyExists: db.Comments.Where(x => ActivePostID.Contains(x.post_id)),
                                        GetKey: x => x.fb_id + "_" + x.post_id,
                                        Updater: (dbItem, memItem) =>
                                        {
                                            if (dbItem.message != memItem.message)
                                            {
                                                dbItem.message = memItem.message;
                                                return true;
                                            }
                                            return false;
                                        }
                                    );

                    foreach (var comment in postComments)
                    {
                        commentUpdater.Process(comment.id + "_" + comment.post_id, () => new Comment()
                            {
                                fb_id = comment.id,
                                message = comment.message,
                                post_id = postTranslator[comment.post_id],
                                entity_id = entityTranslator[comment.user_id],
                                date = comment.date,
                                score = Common.Classifier.Classify(comment.message)
                            });
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

                    #region Update likes
                    log.Info("Creating PostLike-UpdateOrInsertBuilder");
                    var likeUpdater = new UpdateOrInsertBuilder<PostLike>(
                                        AlreadyExists: db.PostLikes.Where(x => ActivePostID.Contains(x.post_id)),
                                        GetKey: x => x.post_id + "_" + x.entity_id,
                                        Updater: (dbItem, memItem) => false);

                    log.Info("Processing PostLikes");
                    foreach (var like in postLikes)
                    {
                        likeUpdater.Process(postTranslator[like.post_id] + "_" + entityTranslator[like.user_id], () => new PostLike()
                        {
                            post_id = postTranslator[like.post_id],
                            entity_id = entityTranslator[like.user_id],
                        });
                    }
                    likeUpdater.SyncDatabase(10000, "dbo.[PostLikes]", "id", x => new
                    {
                        id = (int?)null,
                        post_id = x.post_id,
                        entity_id = x.entity_id,
                    });
                    #endregion

                    log.Info("Marking scraped");
                    Parallel.ForEach(scrapedPosts, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, scrapedPost =>
                    {
                        if (!scrapedPost.scraped)
                            DatabaseTools.ExecuteNonQuery("UPDATE dbo.[Posts] SET scraped = 1 WHERE id = @id", new SqlParameter("id", scrapedPost.id));
                    });
                }
            }
        }
    }
}
