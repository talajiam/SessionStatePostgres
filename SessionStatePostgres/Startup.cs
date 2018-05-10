using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(SessionStatePostgres.Startup))]
namespace SessionStatePostgres
{
    public partial class Startup {
        public void Configuration(IAppBuilder app) {
            ConfigureAuth(app);
        }
    }
}
