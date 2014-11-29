using Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FacebookDebat.Controllers
{
    public class StatisticsController : Controller
    {
        public JsonResult GetGraph()
        {
            var nodes = Tools.ExecuteReader(@"select 'page', id, name from dbo.Page where enabled = 1 union
                                               select 'user', id, name from dbo.[User] u where (select count(*) from dbo.Comment where user_id = u.id and score < -2) > 1",
                r => new
                {
                    id = r.GetInt32(1),
                    name = r.GetString(2),
                    group = r.GetString(0)
                });//.ToDictionary(x => x.id.ToString(), x => x);
            var links = Tools.ExecuteReader(@"select pa.id, co.user_id, count(*)
                                                    from dbo.Page pa
                                                    inner join dbo.Post po on po.page_id = pa.id
                                                    inner join dbo.Comment co on co.post_id = po.id
                                                    inner join dbo.[User] u on co.user_id = u.id and (select count(*) from dbo.Comment where user_id = u.id and score < -2) > 1
                                                    where enabled = 1
                                                    group by pa.id, co.user_id",
                r => new
                {
                    page = r.GetInt32(0),
                    user = r.GetInt32(1),
                    value = r.GetInt32(2)
                });
            return Json(new { nodes, links }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult CommentsPrDate()
        {
            var reader = Tools.ExecuteReader(@"select top 30
	                            DATEADD(dd, 0, DATEDIFF(dd, 0, date)) as date,
								sum(case when score >= -2 then 1 else 0 end) as comments,
								sum(case when score < -2 then 1 else 0 end) as bad_comments
                            from dbo.Comment
                            group by DATEADD(dd, 0, DATEDIFF(dd, 0, date))
                            order by DATEADD(dd, 0, DATEDIFF(dd, 0, date)) DESC",
                r => new {
                    date = r.GetDateTime(0),
                    comments = r.GetInt32(1),
                    bad_comments = r.GetInt32(2)
                });
            reader.Reverse();
            return Json(reader, JsonRequestBehavior.AllowGet);
        }
        public JsonResult CommentsPrPage()
        {
            var reader = Tools.ExecuteReader(@"select
	                                            name,
	                                            count(distinct c.id) as comments,
	                                            sum(case when c.score < -2 then 1 else 0 end) as bad_comments
                                            from FacebookDebat.dbo.Page pa
                                            inner join dbo.Post po on po.page_id = pa.id
                                            inner join dbo.Comment c on po.id = c.post_id
                                            group by pa.id, pa.name",
                r => new
                {
                    name = r.GetString(0),
                    comments = r.GetInt32(1),
                    bad_comments = r.GetInt32(2)
                });
            return Json(reader, JsonRequestBehavior.AllowGet);
        }
    }
}