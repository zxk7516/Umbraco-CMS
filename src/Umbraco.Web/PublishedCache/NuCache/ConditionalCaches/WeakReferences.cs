using System;

namespace Umbraco.Web.PublishedCache.NuCache.ConditionalCaches
{
    /// <summary>
    /// Provides static methods for creating WeakReferences objects.
    /// </summary>
    static class WeakReferences
    {
        /// <summary>
        /// Creates a new WeakReferences combining 1 reference.
        /// </summary>
        /// <typeparam name="T">The type of the referenced object.</typeparam>
        /// <param name="r1">The referenced object.</param>
        /// <returns>A new WeakReferences referencing one object.</returns>
        /// <remarks>Does not make sense per-se as it would make more sense to use
        /// a WeakReference{T} but needed so it can be used as a key.</remarks>
        public static WeakReferences<T> Create<T>(T r1)
            where T : class
        {
            return new WeakReferences<T>(r1);
        }

        /// <summary>
        /// Creates a new WeakReferences combining 2 references.
        /// </summary>
        /// <typeparam name="T">The type of the referenced objects.</typeparam>
        /// <param name="r1">The first referenced object.</param>
        /// <param name="r2">The second referenced object.</param>
        /// <returns>A new WeakReferences combining references to the two objects.</returns>
        public static WeakReferences<T> Create<T>(T r1, T r2)
            where T : class
        {
            return new WeakReferences<T>(r1, r2);
        }

        /// <summary>
        /// Creates a new WeakReferences combining 3 references.
        /// </summary>
        /// <typeparam name="T">The type of the referenced objects.</typeparam>
        /// <param name="r1">The first referenced object.</param>
        /// <param name="r2">The second referenced object.</param>
        /// <param name="r3">The third referenced object.</param>
        /// <returns>A new WeakReferences combining references to the three objects.</returns>
        public static WeakReferences<T> Create<T>(T r1, T r2, T r3)
            where T : class
        {
            return new WeakReferences<T>(r1, r2, r3);
        }
    }
}
