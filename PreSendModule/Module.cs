using System.Web;

namespace PreSendModule
{
    // Subscribing to PreSendRequestContent is enough to trigger the bug. The handler does nothing.
    public sealed class Module : IHttpModule
    {
        public void Init(HttpApplication app)
        {
            app.PreSendRequestContent += (sender, e) => { };
        }

        public void Dispose()
        {
        }
    }
}
