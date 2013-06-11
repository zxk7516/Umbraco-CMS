using System;
using System.Linq;

namespace Umbraco.Web.PublishedCache.NuCache.ConditionalCaches
{
    /// <summary>
    /// Represents a set of typed weak references.
    /// </summary>
    /// <typeparam name="T">The type of the referenced objects.</typeparam>
    class WeakReferences<T>
        where T : class
    {
        private readonly WeakReference<T>[] _wrefs;
        private readonly int _hash;

        internal WeakReferences(params T[] refs)
        {
            _wrefs = refs.Select(r => new WeakReference<T>(r)).ToArray();

            // http://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-an-overridden-system-object-gethashcode
            _hash = 17;
            unchecked
            {
                _hash = refs.Aggregate(_hash, (current, obj) => current * 23 + obj.GetHashCode());
            }
        }

        public bool IsAlive
        {
            get
            {
                // ensure that each wref is alive, and accumulate
                // refs in order to make sure that no wref can die
                // after we've checked it
                var refs = new T[_wrefs.Length];
                return _wrefs
                    .Select((t, i) => t.TryGetTarget(out refs[i]))
                    .All(x => x);
            }
        }

        // implement Equals and GetHashCode for 

        public override bool Equals(object obj)
        {
            // x.Equals(null) == false
            // x.Equals(x) == true
            var o = obj as WeakReferences<T>;
            if ((object)o == null) return false;
            if (ReferenceEquals(this, obj)) return true;

            // must contain the same number of wrefs
            if (_wrefs.Length != o._wrefs.Length) return false;

            for (var i = 0; i < _wrefs.Length; i++)
            {
                T t, ot;
                // if at least one wref is not active anymore
                // we cannot compare refs - and we are not comparing
                // the object to itself - so what shall we do FIXME?!
                if (_wrefs[i].TryGetTarget(out t) == false || o._wrefs[i].TryGetTarget(out ot) == false)
                    //return ReferenceEquals(o, obj);
                    return false;

                // else both refs must be equal
                if (t != ot) return false;
            }

            // all wrefs are active and all refs are equal
            return true;
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        //public static bool operator ==(WeakReferences<T> a, WeakReferences<T> b)
        //{
        //    // both are null or both are the same instance
        //    if (ReferenceEquals(a, b)) return true;
        //    // one is null
        //    if ((object)a == null || (object)b == null) return false;
        //    // values
        //    return a.Value == b.Value;
        //}

        //public static bool operator !=(WeakReferences<T> a, WeakReferences<T> b)
        //{
        //    return (a == b) == false;
        //}
    }
}
