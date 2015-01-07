using System.Xml.XPath;
using Umbraco.Core.Models;

namespace Umbraco.Web.PublishedCache
{
    public interface IPublishedMemberCache
    {
        IPublishedContent GetByProviderKey(object key);
        IPublishedContent GetById(int memberId);
        IPublishedContent GetByUsername(string username);
        IPublishedContent GetByEmail(string email);
        IPublishedContent GetByMember(IMember member);

        // fixme - what-if the node does not exist?
        XPathNavigator CreateNodeNavigator(int id, bool preview);
    }
}
