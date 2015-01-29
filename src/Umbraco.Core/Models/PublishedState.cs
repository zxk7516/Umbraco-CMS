namespace Umbraco.Core.Models
{
    public enum PublishedState
    {
        // when a content version is loaded, its state is one of those two:

        // content version is published
        // or has been published
        Published,

        // content version is not published
        // or has been unpublished
        Unpublished,

        // in addition, once it's been saved, its state can also be:

        // content version has been saved
        Saved

        // but soon as it will be reloaded, it will be back to Unpublished
        //
        // so .Saved is a transitional state really - this is because .Unpublished
        // is used to indicate that the content (as a whole, not a specific version)
        // has been unpublished - and so if we load a .Published version, apply some
        // changes and save, it cannot be .Unpublished (or that would unpublish the
        // whole content) and has to be .Saved
    }
}