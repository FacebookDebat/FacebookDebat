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
    public class CommenterController : Controller
    {

        public class Commenter
        {
            public string name;
            public List<Dictionary<string, object>> dates;
            public List<Dictionary<string, object>> words;
            public List<Dictionary<string, object>> pages;
        }
        // GET: Commenter
        public ActionResult Page(int id)
        {
            var name = DatabaseTools.ExecuteSingle("select name from Entities where id = @id", r => r.GetString(0), new SqlParameter("id", id));

            var dateList = DatabaseTools.ExecuteReaderAsync(
               @"select CAST(date AS DATE) as date, count(*) as cnt from comments
                        where entity_id = @id
                        group by CAST(date AS DATE)", new SqlParameter("id", id));

            var pageList = DatabaseTools.ExecuteReaderAsync(@"
                        select e.id, e.name, count(*) as cnt
                        from Comments c
                        inner join Posts p on c.post_id = p.id
                        inner join Entities e on p.entity_id = e.id
                        where c.entity_id =  @id
                        group by e.id, e.name
                        order by cnt desc", new SqlParameter("id", id));

            var wordList = DatabaseTools.ExecuteReaderAsync(@"
                        select top 100 w.id, w.word, count(*) as cnt
                            from Comments c
                            inner join CommentWords cw on cw.comment_id = c.id 
                            inner join Words w on cw.word_id = w.id
                            where c.entity_id = @id
                            and stop = 0
                            group by w.id, w.word
                            order by cnt desc", new SqlParameter("id", id));


            Task.WaitAll(wordList, pageList, dateList);

            return View(new Commenter { name = name, dates = dateList.Result, pages = pageList.Result, words = wordList.Result });
        }
    }
}