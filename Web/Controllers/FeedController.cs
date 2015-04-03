using Common;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;

namespace Web.Controllers
{
    public class FeedController : ApiController
    {

        public IEnumerable<Dictionary<string, object>> Get(DateTime? since = null)
        {
            if (since == null)
            {
                return DatabaseTools.ExecuteReader(@"select top 100 c.message as comment, p.message as post, c.date, ce.name as commenter_name, ce.id as commenter_id, pe.name as post_name, pe.id as post_id, c.score
                                    from dbo.Comments c
                                    inner join dbo.Posts p on c.post_id = p.id
                                    inner join dbo.Entities ce on c.entity_id = ce.id
                                    inner join dbo.Entities pe on p.entity_id = pe.id
                                    order by c.date desc");
            }
            else
            {
                return DatabaseTools.ExecuteReader(@"select c.message as comment, p.message as post, c.date, ce.name as commenter_name, ce.id as commenter_id, pe.name as post_name, pe.id as post_id, c.score
                                    from dbo.Comments c
                                    inner join dbo.Posts p on c.post_id = p.id
                                    inner join dbo.Entities ce on c.entity_id = ce.id
                                    inner join dbo.Entities pe on p.entity_id = pe.id
                                    where c.date >= @date
                                    order by c.date desc", new SqlParameter("date", since));
            }
        }
    }
}