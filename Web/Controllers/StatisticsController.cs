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