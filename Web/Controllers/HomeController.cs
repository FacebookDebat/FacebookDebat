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
            var result = DatabaseTools.ExecuteReader(@"select top 100 c.message as comment, p.message as post, c.date, ce.name as commenter_name, pe.name as post_name, c.score
                                    from dbo.Comments c
                                    inner join dbo.Posts p on c.post_id = p.id
                                    inner join dbo.Entities ce on c.entity_id = ce.id
                                    inner join dbo.Entities pe on p.entity_id = pe.id
                                    order by c.date desc");
            return View(result);
        }

        public ActionResult Graphs()
        {
            return View();
        }
        public ActionResult Network()
        {
            return View();
        }
    }
}