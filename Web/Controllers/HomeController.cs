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
        public static List<T> ExecuteReader<T>(string s, Func<SqlDataReader, T> f, params SqlParameter[] parms) {
            var rv = new List<T>();
            SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["FacebookDebat"].ConnectionString);
            conn.Open();
            var command = conn.CreateCommand();
            command.CommandText = s;
            command.Parameters.AddRange(parms);
            var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rv.Add(f(reader));
            }
            return rv;
        }

        public ActionResult Index()
        {
            var reader = ExecuteReader(@"select
	                            DATEADD(dd, 0, DATEDIFF(dd, 0, date)) as date,
	                            count(*)
                            from dbo.Comment
                            group by DATEADD(dd, 0, DATEDIFF(dd, 0, date))
                            order by DATEADD(dd, 0, DATEDIFF(dd, 0, date))",
                r => Tuple.Create(r.GetDateTime(0), r.GetInt32(1)));
            return View(reader);
        }



    }
}