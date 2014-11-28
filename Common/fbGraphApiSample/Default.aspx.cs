using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text;
using Facebook;
using Newtonsoft.Json.Linq;

public partial class _Default : System.Web.UI.Page 
{
    public bool IsUserLoggedIn = false;

    protected void Page_Load(object sender, EventArgs e)
    {

        try
        {
            StringBuilder sb = new StringBuilder();

            var args = FacebookGraphAPI.GetUserFromCookie(Request.Cookies, "YOUR_APP_ID", "YOUR_APP_SECRET");

            if (args != null)
            {
                IsUserLoggedIn = true;

                var facebook = new FacebookGraphAPI(args["access_token"]);

                var user = facebook.GetObject("me", null);
                sb.Append("<br/>Get Object <br/>");
                sb.Append(user["name"]);

                var users = facebook.GetObjects(null, "btaylor", "amiune");
                sb.Append("<br/>Get Objects <br/>");
                sb.Append(users["btaylor"]["name"]);
                sb.Append(users["amiune"]["name"]);

                var friends = facebook.GetConnections("me", "friends", null);
                sb.Append("<br/>Get Connections <br/>");
                foreach (var friend in friends["data"]) sb.Append(friend["name"]);


                // You must first grant permissions to publish to the stream
                //var data = new Dictionary<string, string>();
                //data.Add("message", "testing facebook graph api");
                //var putobject = facebook.PutObject("me", "feed", data);

                facebookdata.InnerHtml = sb.ToString();
            }


        }
        catch (FacebookGraphAPIException ex)
        {
            //facebookdata.InnerHtml = ex.Type + " = " + ex.Message + "<br/><br/>" + ex.StackTrace;
        }
        catch (Exception ex)
        {
            //facebookdata.InnerHtml = ex.Message + "<br/><br/>" + ex.StackTrace;
        }


    }
}
