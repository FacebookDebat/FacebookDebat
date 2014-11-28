<%@ Page Language="C#" AutoEventWireup="true"  CodeFile="CanvasSample.aspx.cs" Inherits="_CanvasSample" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head id="Head1" runat="server">
    <title></title>
</head>
<body>
    
    <!-- the facebook login button -->
    <!-- if is not logged in send to authorization page -->
    <% if (!IsUserLoggedIn) { %>
    Requesting your permission...
    <script type="text/javascript">
        window.top.location = "https://graph.facebook.com/oauth/authorize?client_id=YOUR_APP_ID&redirect_uri=http://apps.facebook.com/YOUR_APP_NAME/";
    </script>
    <% } %>
    
    <!-- a div to show the data returned from facebook  -->
    <div id="facebookdata" runat="server"></div>
    
    <!-- the code needed to load the facebook library asyncronous  -->
    <div id="fb-root"></div>
    <script type="text/javascript">
      window.fbAsyncInit = function() {
        FB.init({ appId: 'YOUR_APP_ID', status: true, cookie: true, xfbml: true });
      };
      (function() {
        var e = document.createElement('script');
        e.type = 'text/javascript';
        e.src = document.location.protocol + '//connect.facebook.net/en_US/all.js';
        e.async = true;
        document.getElementById('fb-root').appendChild(e);
    } ());
    </script>
   
</body>
</html>