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

        static JToken GetGraphApiReply(string url, string parms)
        {
            var client = new WebClient();
            string response = client.DownloadString("https://graph.facebook.com/v2.2/" + url + "?access_token=" + ConfigurationManager.AppSettings["FBAccessToken"] + "&format=json&method=get&pretty=0&" + parms);
            return JObject.Parse(response);
        }

        private static void ExtendToken()
        {
            var client = new WebClient();
            string response = client.DownloadString(string.Format(
                @"https://graph.facebook.com/oauth/access_token?             
                    client_id={0}&
                    client_secret={1}&
                    grant_type=fb_exchange_token&
                    fb_exchange_token={2}",
                    ConfigurationManager.AppSettings["FBAppID"],
                    ConfigurationManager.AppSettings["FBAppSecret"],
                    ConfigurationManager.AppSettings["FBAccessToken"]));
        }


        static DateTime JsonToTime(JToken token)
        {
            return DateTime.Parse(token.ToString());
        }

        static void Main(string[] args)
        {
            if (!args.Contains("extendtoken"))
            {
                ExtendToken();
                return;
            }

            bool getNames = bool.Parse(ConfigurationManager.AppSettings["GetNames"]);
            bool getPosts = bool.Parse(ConfigurationManager.AppSettings["GetPosts"]);
            bool getComments = bool.Parse(ConfigurationManager.AppSettings["GetComments"]);

            if (getNames)
                GetPageInfo();

            if (getPosts)
                GetPosts();

            if (getComments)
                GetComments();
        }


        private static void GetPageInfo()
        {
            Console.WriteLine("Getting page infos");
            using (var db = new FacebookDebatEntities())
            {
                db.Configuration.AutoDetectChangesEnabled = false;
                foreach (var page in db.Pages.Where(x => x.enabled).ToList())
                {
                    var objName = GetGraphApiReply(page.fb_id + "/", "fields=name");
                    page.name = objName["name"].ToString();
                    db.Entry(page).State = System.Data.Entity.EntityState.Modified;
                    Console.WriteLine("Found name " + page.name + " for " + page.fb_id);
                }
                db.SaveChanges();
            }
        }

        private static void GetPosts()
        {
            using (var db = new FacebookDebatEntities())
            {
                db.Configuration.AutoDetectChangesEnabled = false;
                // Get posts for each page
                foreach (var page in db.Pages.Where(x => x.enabled).ToList())
                {
                    Console.WriteLine("Getting posts for " + page.name);

                    try
                    {

                        var obj = GetGraphApiReply(page.fb_id + "/posts", "fields=id,message,created_time");

                        var fb_posts = obj["data"].Where(x => x["message"] != null)
                            .Select(x =>
                                new
                                {
                                    message = x["message"].ToString(),
                                    id = x["id"].ToString(),
                                    date = JsonToTime(x["created_time"])
                                }).ToList();

                        foreach (var fb_post in fb_posts)
                        {
                            var post = db.Posts.SingleOrDefault(x => x.fb_id == fb_post.id);

                            if (post == null)
                            {
                                post = new Post()
                                {
                                    fb_id = fb_post.id,
                                    message = fb_post.message,
                                    Page = page,
                                    date = fb_post.date
                                };
                                db.Posts.Add(post);
                            }
                            else
                                post.message = fb_post.message;
                        }
                        db.SaveChanges();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }
            }
        }

        private static void GetComments()
        {
            var scrapedPosts = new List<Post>();
            var updatedComments = new List<Comment>();

            var con_str = ConfigurationManager.ConnectionStrings["FacebookDebat"].ToString();

            Console.WriteLine("Getting comments");
            var newComments = new List<NewComment>();
            var newUsers = new List<User>();
            var newUsersHashSet = new HashSet<string>();


            using (var db = new FacebookDebatEntities())
            {
                var existingUsers = new HashSet<string>(db.Users.Select(x => x.fb_id));
                var existingComments = db.Comments.ToDictionary(x => x.fb_id, x => x);

                int i = 0;
                object iLock = new object();

                var posts = db.Posts.Where(x => !x.scraped).ToList();
                var maxPosts = int.Parse(ConfigurationManager.AppSettings["MaxPosts"]);
                if (maxPosts > -1)
                    posts = posts.OrderBy(x => x.id).Take(maxPosts).ToList();

                var fbFailed = false;

                Parallel.ForEach(posts, (post) =>
                {
                    lock (iLock)
                        Console.WriteLine("Post " + i++ + "/" + posts.Count);

                    if (fbFailed)
                        return;

                    JToken fb_comment_token;
                    try
                    {
                        // Get from FB
                        fb_comment_token = GetGraphApiReply(post.fb_id + "/comments",
                            "fields=id,message,from,created_time&filter=stream&offset=0&limit=1000")["data"];
                    }
                    catch (Exception e)
                    {
                        fbFailed = true;
                        Console.WriteLine("Being ignored by Facebook. Aborting.");
                        Console.WriteLine(e.Message);

                        return;
                    }


                    var fb_comments = fb_comment_token.Select(x => new
                    {
                        id = x["id"].ToString(),
                        message = x["message"].ToString(),
                        user_id = x["from"]["id"].ToString(),
                        user_name = x["from"]["name"].ToString(),
                        date = JsonToTime(x["created_time"])
                    });

                    foreach (var fb_comment in fb_comments)
                    {
                        if (!existingUsers.Contains(fb_comment.user_id) && !newUsersHashSet.Contains(fb_comment.user_id))
                        {
                            lock (newUsers)
                            {
                                newUsers.Add(new User()
                                {
                                    name = fb_comment.user_name,
                                    fb_id = fb_comment.user_id
                                });
                                newUsersHashSet.Add(fb_comment.user_id);
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
                foreach (var comment in updatedComments)
                    db.Database.ExecuteSqlCommand("UPDATE dbo.[Comment] SET message = @comment WHERE id = @id", new SqlParameter("id", comment.id), new SqlParameter("comment", comment.message));
            }

            // Add all new users
            Console.WriteLine("Inserting " + newUsers.Count + " users");

            var InsertUserTable = new DataTable("Users");
            InsertUserTable.Columns.Add("id", typeof(int));
            InsertUserTable.Columns.Add("fb_id", typeof(string));
            InsertUserTable.Columns.Add("name", typeof(string));

            var userInsertedCount = 0;
            var userBulkSize = int.Parse(ConfigurationManager.AppSettings["UserBulkSize"]);
            foreach (var userList in newUsers.Chunk(userBulkSize))
            {
                userInsertedCount += userBulkSize;
                InsertUserTable.Rows.Clear();
                foreach (var user in userList)
                    InsertUserTable.Rows.Add(null, user.fb_id, user.name);

                Console.WriteLine("Inserting " + userInsertedCount + "/" + newUsers.Count + " new users");
                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con_str, SqlBulkCopyOptions.KeepIdentity & SqlBulkCopyOptions.KeepNulls))
                {
                    bulkCopy.DestinationTableName = "dbo.[User]";
                    bulkCopy.WriteToServer(InsertUserTable);
                }
            }


            // Add all commentWords
            Console.WriteLine("Inserting " + newComments.Count + " comments");

            var InsertNewCommentTable = new DataTable("NewCommentWords");
            InsertNewCommentTable.Columns.Add("id", typeof(int));
            InsertNewCommentTable.Columns.Add("fb_id", typeof(string));
            InsertNewCommentTable.Columns.Add("message", typeof(string));
            InsertNewCommentTable.Columns.Add("post_id", typeof(int));
            InsertNewCommentTable.Columns.Add("user_id", typeof(int));
            InsertNewCommentTable.Columns.Add("date", typeof(DateTime));
            InsertNewCommentTable.Columns.Add("score", typeof(int));
            InsertNewCommentTable.Columns.Add("scored", typeof(int));

            var commentInsertedCount = 0;
            var idTranslator = new FacebookDebatEntities().Users.ToDictionary(x => x.fb_id, x => x.id);
            var commentBulkSize = int.Parse(ConfigurationManager.AppSettings["CommentBulkSize"]);
            foreach (var comments in newComments.Chunk(commentBulkSize))
            {
                commentInsertedCount += commentBulkSize;
                InsertNewCommentTable.Rows.Clear();
                foreach (var comment in comments)
                    InsertNewCommentTable.Rows.Add(null, comment.fb_id, comment.message, comment.post_id, idTranslator[comment.user_fb_id], comment.date, comment.score, 1);

                Console.WriteLine("Inserting " + commentInsertedCount + "/" + newComments.Count + " new comments");
                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con_str, SqlBulkCopyOptions.KeepIdentity & SqlBulkCopyOptions.KeepNulls))
                {
                    bulkCopy.DestinationTableName = "dbo.[Comment]";
                    bulkCopy.WriteToServer(InsertNewCommentTable);
                }
            }

            using (var db = new FacebookDebatEntities())
            {
                Console.WriteLine("Updated " + scrapedPosts.Count + " posts.");
                foreach (var post in scrapedPosts)
                    db.Database.ExecuteSqlCommand("UPDATE dbo.[Post] SET scraped = 1 WHERE id = @id", new SqlParameter("id", post.id));
            }

            Console.ReadLine();
        }

    }
}
