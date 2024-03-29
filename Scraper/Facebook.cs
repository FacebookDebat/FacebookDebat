﻿using Newtonsoft.Json.Linq;
using System;
using Common;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Scraper
{
    class Facebook
    {
        string token;
        public Facebook()
        {
            token = GetToken();
        }

        private static string GetToken()
        {
            var client = new WebClient();
            var requestUri = string.Format(
                @"https://graph.facebook.com/oauth/access_token?             
                    client_id={0}&
                    client_secret={1}&
                    grant_type=client_credentials",
                    ConfigurationManager.AppSettings["FBAppID"],
                    ConfigurationManager.AppSettings["FBAppSecret"]).Replace("\r", "").Replace("\n", "");
            string response = client.DownloadString(requestUri);
            return response.Replace("access_token=", "");
        }

        List<JToken> GetGraphApiReplyUntillEnd(string url, string parms)
        {
            var replies = new List<JToken>();
            replies.Add(GetGraphApiReply(url, parms));
            while (Nullify.Get(replies.Last(), x => x["paging"], x => x["cursors"], x => x["after"]) != null)
            {
                var reply = GetGraphApiReply(url, parms + "&after=" + replies.Last()["paging"]["cursors"]["after"]);
                replies.Add(reply);
            }
            return replies;
        }

        JToken GetGraphApiReply(string url, string parms)
        {
            var client = new WebClient();
            var request = "https://graph.facebook.com/v2.2/" + url + "?access_token=" + token + "&format=json&method=get&pretty=0&" + parms;
            var response = client.DownloadString(request);
            return JObject.Parse(response);
        }
        DateTime JsonToTime(JToken token)
        {
            return DateTime.Parse(token.ToString());
        }

        public struct Post
        {
            public string message;
            public string id;
            public DateTime date;
            public int shares;
        }
        public List<Post> GetPosts(string page_fb_id, int lookbackDays, DateTime dateFrom)
        {
            var limit = ConfigurationManager.AppSettings["PagePostLimit"];
            var until = dateFrom;
            var since = until.AddDays(-lookbackDays);
            var obj = GetGraphApiReply(page_fb_id + "/posts", "fields=id,message,created_time,shares&limit=" + limit + "&since=" + since.ToUnixTimestamp() + "&until=" + until.ToUnixTimestamp());

            var fb_posts = obj["data"].Where(x => x["message"] != null)
                .Select(x =>
                {
                    var shareElement = Nullify.Get(x, y => y["shares"], y => y["count"]);

                    return new Post
                    {
                        message = x["message"].ToString(),
                        id = x["id"].ToString(),
                        date = JsonToTime(x["created_time"]),
                        shares = shareElement == null ? 0 : int.Parse(shareElement.ToString())
                    };
                }).ToList();
            return fb_posts;
        }


        public struct PostLike
        {
            public string post_id;
            public string user_id;
            public string user_name;
        }
        public List<PostLike> GetLikes(string post_fb_id)
        {
            var likes = GetGraphApiReplyUntillEnd(post_fb_id + "/likes",
                    "fields=id,name&offset=0&limit=1000").SelectMany(y => y["data"]
            .Select(x => new PostLike
            {
                post_id = post_fb_id,
                user_id = x["id"].ToString(),
                user_name = x["name"].ToString(),
            }));

            return likes.ToList();
        }
        public struct Comment
        {
            public string post_id;
            public string message;
            public string id;
            public string user_id;
            public string user_name;
            public DateTime date;
        }
        public List<Comment> GetComments(string post_fb_id)
        {
            var fb_comments = GetGraphApiReplyUntillEnd(post_fb_id + "/comments",
                    "fields=id,message,from,created_time&filter=stream&offset=0&limit=1000").SelectMany(y => y["data"]
            .Select(x => new Comment
            {
                post_id = post_fb_id,
                id = x["id"].ToString(),
                message = x["message"].ToString(),
                user_id = x["from"]["id"].ToString(),
                user_name = x["from"]["name"].ToString(),
                date = JsonToTime(x["created_time"])
            }));

            return fb_comments.ToList();
        }

        public struct User
        {
            public string fb_id;
            public string name;
        }
        public User GetUser(string user_fb_id)
        {
            var objName = GetGraphApiReply(user_fb_id, "fields=name");
            return new User
            {
                fb_id = objName["id"].ToString(),
                name = objName["name"].ToString()
            };
        }

    }
}
