<%@ Page Language="C#" AutoEventWireup="true"  CodeFile="Default.aspx.cs" Inherits="_Default" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head id="Head1" runat="server">
    <title></title>
</head>
<body>
    
    <!-- the facebook login button -->
    <fb:login-button autologoutlink="true"></fb:login-button>
    
    <!-- a div to show the data returned from facebook  -->
    <div id="facebookdata" runat="server"></div>
    
    <!-- the code needed to load the facebook library asyncronous  -->
    <div id="fb-root"></div>
    <script type="text/javascript">
      window.fbAsyncInit = function() {
        FB.init({ appId: 'YOUR_APP_ID', status: true, cookie: true, xfbml: true });
        FB.Event.subscribe('<% if(IsUserLoggedIn)Response.Write("auth.logout"); else Response.Write("auth.login"); %>', function(response) {
            window.location.reload();
        });
      };
      (function() {
        var e = document.createElement('script');
        e.type = 'text/javascript';
        e.src = document.location.protocol + '//connect.facebook.net/en_US/all.js';
        e.async = true;
        document.getElementById('fb-root').appendChild(e);
    } ());
    </script>
    
    <!-- the code needed to grant facebook permissions -->
    <a href="#" onclick="grantPermissions()">Grant permissions.</a> 
    <script type="text/javascript">
        function grantPermissions() {
            FB.login(function(response) {
                if (response.session) {
                    if (response.perms) {
                        // user is logged in and granted some permissions.
                        // perms is a comma separated list of granted permissions
                    } else {
                        // user is logged in, but did not grant any permissions
                    }
                } else {
                    // user is not logged in
                }
            }, { perms: 'read_stream,publish_stream,offline_access' });
        }
    </script>
   
</body>
</html>