using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(FacebookDebat.Startup))]
namespace FacebookDebat
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
