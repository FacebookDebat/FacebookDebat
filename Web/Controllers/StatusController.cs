using Common;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FacebookDebat.Controllers
{
    public class StatusController : Controller
    {
        public class PageResult
        {
            public List<Dictionary<string, object>> words;
            public List<Dictionary<string, object>> commentators;
        }

        public ActionResult Page(int id)
        {
            var wordList = DatabaseTools.ExecuteReaderAsync(@"
                        select top 100 w.id, w.word, count(*) as cnt
                            from Comments c
                            inner join CommentWords cw on cw.comment_id = c.id 
                            inner join Words w on cw.word_id = w.id
                            where c.post_id = @postId
                            and stop = 0
                            group by w.id, w.word
                            order by cnt desc", new SqlParameter("postId", id));

            var commentators = DatabaseTools.ExecuteReaderAsync(@"select e.id, e.name, count(*) as cnt
                                    from Comments c
                                    inner join Entities e on e.id = c.entity_id
                                    where c.post_id = @postId
                                    group by e.id, e.name
                                    order by cnt desc", new SqlParameter("postId", id));

            Task.WaitAll(commentators, wordList);

            return View(new PageResult { commentators = commentators.Result, words = wordList.Result });
        }
    }
}