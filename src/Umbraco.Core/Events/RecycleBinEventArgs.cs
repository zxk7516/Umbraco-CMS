using System;

namespace Umbraco.Core.Events
{
    public class RecycleBinEventArgs : CancellableEventArgs
    {
        public RecycleBinEventArgs(Guid nodeObjectType, bool emptiedSuccessfully)
            : base(false)
        {
            NodeObjectType = nodeObjectType;
            RecycleBinEmptiedSuccessfully = emptiedSuccessfully;
        }

        public RecycleBinEventArgs(Guid nodeObjectType)
            : base(true)
        {
            NodeObjectType = nodeObjectType;
        }

        /// <summary>
        /// Gets the Id of the node object type of the items 
        /// being deleted from the Recycle Bin.
        /// </summary>
        public Guid NodeObjectType { get; private set; }
        
        /// <summary>
        /// Boolean indicating whether the Recycle Bin was emptied successfully
        /// </summary>
        public bool RecycleBinEmptiedSuccessfully { get; private set; }

        /// <summary>
        /// Boolean indicating whether this event was fired for the Content's Recycle Bin.
        /// </summary>
        public bool IsContentRecycleBin
        {
            get { return NodeObjectType == new Guid(Constants.ObjectTypes.Document); }
        }

        /// <summary>
        /// Boolean indicating whether this event was fired for the Media's Recycle Bin.
        /// </summary>
        public bool IsMediaRecycleBin
        {
            get { return NodeObjectType == new Guid(Constants.ObjectTypes.Media); }
        }
    }
}