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
            var nodes = DatabaseTools.ExecuteReader(@"select 'page', id, name from dbo.Scrapees s where enabled = 1 inner join dbo.Entities e on s.entity_id = e.id union
                                               select 'user', id, name from dbo.[User] u where (select count(*) from dbo.Comment where user_id = u.id and score < -2) > 1");
            var links = DatabaseTools.ExecuteReader(@"select pa.id, co.user_id, count(*)
                                                    from dbo.Page pa
                                                    inner join dbo.Post po on po.page_id = pa.id
                                                    inner join dbo.Comment co on co.post_id = po.id
                                                    inner join dbo.[User] u on co.user_id = u.id and (select count(*) from dbo.Comment where user_id = u.id and score < -2) > 1
                                                    where enabled = 1
                                                    group by pa.id, co.user_id");
            return Json(new { nodes, links }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult CommentsPrDate()
        {
            var reader = DatabaseTools.ExecuteReader(@"select top 30
	                            DATEADD(dd, 0, DATEDIFF(dd, 0, date)) as date,
								sum(case when score >= 1 then 1 else 0 end) as good_comments,
								sum(case when score > -1 and score < 1 then 1 else 0 end) as normal_comments,
								sum(case when score < -1 then 1 else 0 end) as bad_comments,
								sum(case when score is null then 1 else 0 end) as unclassified_comments
                            from dbo.Comments
                            group by DATEADD(dd, 0, DATEDIFF(dd, 0, date))
                            order by DATEADD(dd, 0, DATEDIFF(dd, 0, date)) DESC");
            reader.Reverse();
            return Json(reader, JsonRequestBehavior.AllowGet);
        }
        public JsonResult CommentsPrPage()
        {
            var reader = DatabaseTools.ExecuteReader(@"select
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