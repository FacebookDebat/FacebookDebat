using Common;
using Common.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FacebookDebat.Controllers
{
    public class HomeController : Controller
    {

        public ActionResult Index()
        {
            var result = DatabaseTools.ExecuteReader(@"select top 100 c.message as comment, p.message as post, c.date, ce.name as commenter_name, ce.id as commenter_id, pe.name as post_name, pe.id as post_id, c.score
                                    from dbo.Comments c
                                    inner join dbo.Posts p on c.post_id = p.id
                                    inner join dbo.Entities ce on c.entity_id = ce.id
                                    inner join dbo.Entities pe on p.entity_id = pe.id
                                    order by c.date desc");
            return View(result);
        }
        public JsonResult LiveStream(DateTime? since = null)
        {
            if (since == null)
            {
                return Json(DatabaseTools.ExecuteReader(@"select top 100 c.message as comment, p.message as post, c.date, ce.name as commenter_name, ce.id as commenter_id, pe.name as post_name, pe.id as post_id, c.score
                                    from dbo.Comments c
                                    inner join dbo.Posts p on c.post_id = p.id
                                    inner join dbo.Entities ce on c.entity_id = ce.id
                                    inner join dbo.Entities pe on p.entity_id = pe.id
                                    order by c.date desc"), JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(DatabaseTools.ExecuteReader(@"select c.message as comment, p.message as post, c.date, ce.name as commenter_name, ce.id as commenter_id, pe.name as post_name, pe.id as post_id, c.score
                                    from dbo.Comments c
                                    inner join dbo.Posts p on c.post_id = p.id
                                    inner join dbo.Entities ce on c.entity_id = ce.id
                                    inner join dbo.Entities pe on p.entity_id = pe.id
                                    where c.date >= @date
                                    order by c.date desc", new SqlParameter("date", since)), JsonRequestBehavior.AllowGet);
            }
        }



        public void Stop(int id)
        {
            DatabaseTools.ExecuteNonQuery("update Words set stop = 1 where id = @id", new SqlParameter("id", id));
        }
    }
}