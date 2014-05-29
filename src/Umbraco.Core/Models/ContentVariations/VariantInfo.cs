using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Umbraco.Core.Models.ContentVariations
{
    /// <summary>
    /// A class representing the variant info, whether it is a variant itself or a master doc
    /// </summary>
    public class VariantInfo
    {
        public VariantInfo()
        {
            IsVariant = false;
            VariantIds = new int[] {};
        }

        public VariantInfo(params int[] variantIds)
        {
            VariantIds = variantIds;
            IsVariant = false;
        }

        public VariantInfo(int masterDocId, string key)
        {
            IsVariant = true;
            MasterDocId = masterDocId;
            Key = key;
        }

        public int[] VariantIds { get; set; }

        /// <summary>
        /// Whether or not this is a variant (i.e. not a master doc)
        /// </summary>
        public bool IsVariant { get; private set; }

        /// <summary>
        /// The master doc id if this is a variant
        /// </summary>
        public int MasterDocId { get; private set; }

        /// <summary>
        /// They key stored with this variant when it is a variant.
        /// </summary>
        public string Key { get; private set; }

        ///// <summary>
        ///// The child variants for this node - if it is a master doc
        ///// </summary>
        //public IEnumerable<ChildVariant> ChildVariants { get; private set; }

        public object DeepClone()
        {
            var clone = (VariantInfo)MemberwiseClone();

            return clone;
        }
    }
}
