using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Umbraco.Core.Models.ContentVariations
{
    /// <summary>
    /// A class representing the variant definition(s) of a single node, whether it is a variant itself or a master doc
    /// </summary>
    public class VariantDefinition
    {
        public VariantDefinition(IEnumerable<ChildVariant> childVariants)
        {
            IsVariant = false;
            ChildVariants = childVariants.ToArray();
        }

        public VariantDefinition(int masterDocId, string key)
        {
            IsVariant = true;
            MasterDocId = masterDocId;
            Key = key;
        }

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

        public IEnumerable<ChildVariant> ChildVariants { get; private set; }
    }
}
