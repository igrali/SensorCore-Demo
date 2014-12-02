using Microsoft.Owin;
using Owin;

namespace SensorCoreWeb
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
        }
    }
}