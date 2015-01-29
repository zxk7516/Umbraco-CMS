using System.Web;

namespace Umbraco.Core
{
    internal static class CurrentContextItems
    {
        // note: LogicalGetData vs GetData
        // see http://blog.stephencleary.com/2013/04/implicit-async-context-asynclocal.html
        // GetData is "thread-local" storage and OK here, LogicalGetData flows w/execution context and would be OK for async

        public static void Set(string key, object value)
        {
            if (HttpContext.Current != null)
                HttpContext.Current.Items[key] = value;
            else
                System.Runtime.Remoting.Messaging.CallContext.SetData(key, value);
        }

        public static object Get(string key)
        {
            return HttpContext.Current != null
                ? HttpContext.Current.Items[key]
                : System.Runtime.Remoting.Messaging.CallContext.GetData(key);
        }

        public static void Clear(string key)
        {
            if (HttpContext.Current != null)
                HttpContext.Current.Items.Remove(key);
            else
                System.Runtime.Remoting.Messaging.CallContext.FreeNamedDataSlot(key);
        }
    }
}