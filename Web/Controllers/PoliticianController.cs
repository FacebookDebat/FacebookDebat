using FacebookDebat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Common;
using System.Data.SqlClient;
using Common.Data;
using System.Threading.Tasks;

namespace FacebookDebat.Controllers
{
    public class PoliticianController : Controller
    {
        public struct Politician
        {
            public int id;
            public string name;
            public int postCount;
            public int commentCount;
        }
        public struct Commenter
        {
            public int id;
            public string name;
            public int commentCount;
        }
        public ActionResult List()
        {
            var politicianList = DatabaseTools.ExecuteListReaderAsync(
                        @"select e.id, e.name, count(distinct p.id) as postCount, count(distinct c.id) as commentCount from dbo.Scrapees s
                            inner join dbo.Entities e on e.id = s.entity_id
                            inner join dbo.Posts p on e.id = p.entity_id
                            left join dbo.Comments c on p.id = c.post_id
                            group by e.id, e.name
                            order by postCount desc", (r) => new Politician
                                                    {
                                                        id = r.GetInt32(0),
                                                        name = r.GetString(1),
                                                        postCount = r.GetInt32(2),
                                                        commentCount = r.GetInt32(3)
                                                    });
            var commenterList = DatabaseTools.ExecuteListReaderAsync(
                        @"select top 100 e.id, e.name, count(*) as posts
                            from dbo.Entities e
							left join dbo.Scrapees s on e.id = s.entity_id 
							inner join dbo.Comments c on e.id = c.entity_id
							where s.id is null
                            group by e.id, e.name
                            order by posts desc", (r) => new Commenter
                                                    {
                                                        id = r.GetInt32(0),
                                                        name = r.GetString(1),
                                                        commentCount = r.GetInt32(2)
                                                    });

            Task.WaitAll(commenterList, politicianList);
            return View(Tuple.Create(politicianList.Result, commenterList.Result));
        }

        // GET: Politician
        public ActionResult Page(int id)
        {
            using (var db = new Common.Data.FacebookDebatEntities())
            {
                var entity = db.Entities.Single(x => x.id == id);

                var model = new PoliticianModel()
                {
                    Scrapee = entity.Scrapees.FirstOrDefault(),
                    Entity = entity
                };

                model.Posts = entity.Posts.Select(x => new PoliticianModel.PPost() { Post = x, Count = x.Comments.Count() }).ToList();

                var names = String.Join(",", model.Entity.name.Split(' ').Select(x => "\'" + x.ToLower() + "\'").ToArray());

                var query = string.Format(@"
                                select top 20 w.id, w.word, count(*) as cnt from dbo.CommentWords cw
                                inner join dbo.Words w on cw.word_id = w.id
                                inner join dbo.Comments c on cw.comment_id = c.id
                                inner join dbo.Posts p on c.post_id = p.id
                                where w.stop = 0
                                and not w.word in ({0})
                                and p.entity_id = @entity_id
                                group by w.id, w.word
                                order by cnt desc", names);

                var t1 = DatabaseTools.ExecuteListReaderAsync(query, (r) =>
                                                  {
                                                      return new PoliticianModel.PWord
                                                      {
                                                          Word = new Word { id = r.GetInt32(0), word1 = r.GetString(1) },
                                                          Count = r.GetInt32(2)
                                                      };
                                                  }, new SqlParameter("entity_id", entity.id));

                var t2 = DatabaseTools.ExecuteListReaderAsync(@"
                                        select top 100 e.id, e.name, count(*) as cnt
                                        from Posts p
                                        inner join Comments c on p.id = c.post_id
                                        inner join Entities e on e.id = c.entity_id
                                        where p.entity_id = @entity_id
                                        and c.entity_id <> @entity_id
                                        group by e.id, e.name
                                        order by cnt desc", (r) =>
                                                   {
                                                       return new PoliticianModel.Commenter
                                                       {
                                                           id = r.GetInt32(0),
                                                           name = r.GetString(1),
                                                           Count = r.GetInt32(2)
                                                       };
                                                   }, new SqlParameter("entity_id", entity.id));


                Task.WaitAll(t1, t2);

                model.Words = t1.Result;
                model.Commenters = t2.Result;

                return View(model);
            }
        }
    }
}