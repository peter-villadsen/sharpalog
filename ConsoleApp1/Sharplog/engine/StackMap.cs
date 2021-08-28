using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharplog.Engine
{
    /// <summary>
    /// Map&lt;&gt; implementation that has a parent Map&lt;&gt; where it looks up a value if the value is not in
    /// <c>this</c>
    /// .
    /// <p>
    /// Its behaviour is equivalent to a HashMap that is passed a parent map in its constructor, except that it keeps a reference
    /// to the parent map rather than copying it. It never modifies the parent map. (If the behaviour deviates from this, it is a bug
    /// and must be fixed).
    /// </p><p>
    /// Internally, it has two maps:
    /// <c>self</c>
    /// and
    /// <c>parent</c>
    /// . The
    /// <see cref="StackMap{K, V}.Get(object)"/>
    /// method will look up a key in self and if it doesn't find it, looks
    /// for it in
    /// <c>parent</c>
    /// . The
    /// <see cref="Sharpen.Collections.Put(object, object)"/>
    /// method always adds values to
    /// <c>self</c>
    /// . The parent in turn may also be a StackMap, so some method
    /// calls may be recursive. The
    /// <see cref="StackMap{K, V}.Flatten()"/>
    /// method combines the map with its parents into a single one.
    /// </p><p>
    /// It is used by Jatalog for the scoped contexts where the variable bindings enter and leave scope frequently during the
    /// recursive building of the database, but where the parent Map&lt;&gt; needs to be kept for subsequent recursions.
    /// </p><p>
    /// Methods like
    /// <see cref="StackMap{K, V}.()"/>
    /// ,
    /// <see cref="StackMap{K, V}.Keys()"/>
    /// and
    /// <see cref="StackMap{K, V}.Values()"/>
    /// are required by the Map&lt;&gt; interface that their returned collections be backed
    /// by the Map&lt;&gt;. Therefore, their implementations here will flatten the map first. Once these methods are called StackMap just
    /// becomes a wrapper around the internal HashMap, hence Jatalog avoids these methods internally.
    /// </p><p>
    /// The
    /// <see cref="Sharpen.Collections.Remove(object)"/>
    /// method also flattens
    /// <c>this</c>
    /// to avoid modifying the parent while and the
    /// <see cref="StackMap{K, V}.Clear()"/>
    /// method just sets parent to null
    /// and clears
    /// <c>self</c>
    /// .
    /// </p><p>
    /// I initially just assumed that using the StackMap would be faster, so I tried an implementation with a
    /// <see cref="System.Collections.Generic.Dictionary<object, object>{K, V}"/>
    /// where I just did a
    /// <c>newMap.putAll(parent)</c>
    /// and removed the StackMap entirely. My rough benchmarks showed the StackMap-based implementation to be about 30%
    /// faster than the alternative.
    /// I've also tried a version that extends
    /// <see cref="Sharpen.AbstractMap{K, V}"/>
    /// , but it proved to be significantly slower.
    /// </p>
    /// </summary>
    public class StackMap<K, V>
    {
        private IDictionary<K, V> self;

        private StackMap<K, V> parent;

        public StackMap()
        {
            self = new Dictionary<K, V>();
            this.parent = null;
        }

        public StackMap(StackMap<K, V> parent)
        {
            self = new Dictionary<K, V>();
            this.parent = parent;
        }

        public virtual int Count
        {
            get
            {
                int s = self.Count;
                if (parent != null)
                {
                    // Work around situations where self contains a `key` that's already in `parent`.
                    // These situations shouldn't occur in Jatalog, though
                    foreach (K k in parent.Map.Keys)
                    {
                        if (!self.ContainsKey(k))
                        {
                            s++;
                        }
                    }
                }
                return s;
            }
        }

        public virtual IEnumerable<V> Values
        {
            get
            {
                if (parent != null)
                {
                    self = Flatten();
                    // caveat emptor
                    parent = null;
                }
                return self.Values;
            }
        }

        public virtual IDictionary<K, V> Map
        {
            get
            {
                if (parent != null)
                {
                    self = Flatten();
                    // caveat emptor
                    parent = null;
                }
                return self;
            }
        }

        /// <summary>
        /// Returns a new Map&lt;K,V&gt; that contains all the elements of this map,
        /// but does not have a parent anymore.
        /// </summary>
        /// <remarks>
        /// Returns a new Map&lt;K,V&gt; that contains all the elements of this map,
        /// but does not have a parent anymore.
        /// The returned map is actually a
        /// <c>java.util.HashMap</c>
        /// .
        /// </remarks>
        /// <returns>a new flattened Map.</returns>
        public virtual IDictionary<K, V> Flatten()
        {
            IDictionary<K, V> map = parent != null ? new Dictionary<K, V>(parent.Map) : new Dictionary<K, V>();
            // I don't use map.putAll(this) to avoid relying on entrySet()
            foreach (var e in self)
            {
                map.Add(e.Key, e.Value);
            }
            return map;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("{");
            HashSet<K> keys = new HashSet<K>(self.Keys);
            keys.UnionWith(parent.Map.Keys);
            int s = keys.Count;
            int i = 0;
            foreach (K k in keys)
            {
                sb.Append(k).Append(": ");
                sb.Append(this.Get(k));
                if (++i < s)
                {
                    sb.Append(", ");
                }
            }
            sb.Append("}");
            return sb.ToString();
        }

        public virtual bool ContainsKey(object key)
        {
            if (self.ContainsKey((K)key))
            {
                return true;
            }
            if (parent != null)
            {
                return parent.ContainsKey((K)key);
            }
            return false;
        }

        public virtual V Get(object key)
        {
            self.TryGetValue((K)key, out V value);
            if (value != null)
            {
                return value;
            }
            if (parent != null)
            {
                parent.Map.TryGetValue((K)key, out value);
                return value;
            }
            return default(V);
        }

        public virtual void Clear()
        {
            // We don't want to modify the parent, so we just orphan this
            parent = null;
            self.Clear();
        }

        public override bool Equals(object o)
        {
            if (o == null || !(o is IDictionary))
            {
                return false;
            }
            var that = (IDictionary<object, object>)o;
            return this.Equals(that);
        }

        public override int GetHashCode()
        {
            int h = 0;
            foreach (KeyValuePair<K, V> entry in self)
            {
                h += entry.GetHashCode();
            }
            return h;
        }

        public virtual bool ContainsValue(object value)
        {
            return self.Any(x => x.Value.Equals(value)) || (parent != null && parent.Values.Any(x => x.Equals(value)));
        }

        public void Add(K key, V value)
        {
            if (parent != null && parent.ContainsKey(key))
            {
                throw new InvalidOperationException();
            }

            self.Add(key, value);
        }

        public bool TryGetValue(K key, out V result)
        {
            return self.TryGetValue(key, out result) || (parent?.TryGetValue(key, out result) ?? false);
        }
    }
}