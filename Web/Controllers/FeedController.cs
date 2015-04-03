using Common;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace Web.Controllers
{
    public class FeedController : ApiController
    {
        [HttpGet]
        public IEnumerable<Dictionary<string, object>> Comments(double? lookback = null)
        {
            if (lookback == null)
            {
                return DatabaseTools.ExecuteReader(@"select top 100 c.id as comment_id, c.message as comment, p.message as post, c.date, ce.name as commenter_name, ce.id as commenter_id, pe.name as post_name, pe.id as post_id, c.score
                                    from dbo.Comments c
                                    inner join dbo.Posts p on c.post_id = p.id
                                    inner join dbo.Entities ce on c.entity_id = ce.id
                                    inner join dbo.Entities pe on p.entity_id = pe.id
                                    order by c.date desc");
            }
            else
            {
                return DatabaseTools.ExecuteReader(@"select top 100 c.id as comment_id, c.message as comment, p.message as post, c.date, ce.name as commenter_name, ce.id as commenter_id, pe.name as post_name, pe.id as post_id, c.score
                                    from dbo.Comments c
                                    inner join dbo.Posts p on c.post_id = p.id
                                    inner join dbo.Entities ce on c.entity_id = ce.id
                                    inner join dbo.Entities pe on p.entity_id = pe.id
                                    where c.date <= @date
                                    order by c.date desc", new SqlParameter("date", DateTime.Now.AddSeconds(-lookback.Value)));
            }
        }

        [HttpGet]
        public IEnumerable<Dictionary<string, object>> Posts()
        {
            return DatabaseTools.ExecuteReader(@"select top 100 (select message from Posts where id = p.id) as message, max(pc.date) as last_comment, p.date as post_date, pe.name as post_name, pe.id as poster_id, count(distinct pc.id) as comments, (select count(*) from PostLikes where post_id = p.id) as likes
                                    from dbo.Posts p 
                                    inner join dbo.Entities pe on p.entity_id = pe.id
                                    inner join dbo.Comments pc on pc.post_id = p.id
									group by p.id, pe.id, pe.name, p.date
                                    order by max(pc.date) desc");
        }
    }
}