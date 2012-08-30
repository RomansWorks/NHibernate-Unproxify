using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Omu.ValueInjecter;
using System.Collections.Concurrent;

namespace NHibernateUnproxify
{
    /// <summary>
    /// Provides the SelectWithoutProxies extension method to IQueryable, which can be used to strip proxies easily inside LINQ queries.
    /// </summary>
    public static class NHSelectWithoutProxies
    {
        /// <summary>
        /// Strips proxies from the result set, replacing uninitialized proxies with nulls. Accepts complex objects and circular references. 
        /// NOTE: If you have circular references, remember to set IsReference=true on the DataContract to have them serialized by reference, because this function does preserve circular references.
        /// </summary>
        /// <typeparam name="T">Type of the objects in the result set, this will also be the type returned.</typeparam>
        /// <param name="query">A LINQ query on NHibernate in which some of the objects may contain proxied properties.</param>
        /// <returns>The query results without any proxies.</returns>
        public static IQueryable<T> SelectWithoutProxies<T>(this IQueryable<T> query) where T : new()
        {
            var cache = new ConcurrentDictionary<T, T>();
            return query.Select<T, T>(x => Transform<T>(x, cache));
        }

        /// <summary>
        /// Strips proxies from the result set, replacing uninitialized proxies with nulls, and projects the result to a DTO. Accepts complex objects and circular references. 
        /// NOTE: If you have circular references, remember to set IsReference=true on the DataContract to have them serialized by reference, because this function does preserve circular references.
        /// </summary>
        /// <typeparam name="T">Type of the objects in the result set, this will also be the type returned.</typeparam>
        /// <param name="query">A LINQ query on NHibernate in which some of the objects may contain proxied properties.</param>
        /// <returns>The query results translated to DTOs without any proxies.</returns>
        public static IQueryable<T> SelectWithoutProxies<S,T>(this IQueryable<S> query) where T : new()
        {
            var cache = new ConcurrentDictionary<S, T>();
            return query.Select<S, T>(x => Transform<S,T>(x, cache));
        }

        private static T Transform<T>(T source, IDictionary<T, T> cache) where T : new()
        {
            /// TODO: Solve the issue that this function is called several times on what is probably the same thread... Which may cause unnecessary object creation, and with NHLinq calling this several times per object this might be a problem

            // Already cached?
            if (cache.ContainsKey(source))
                return cache[source];

            // Not cached, create and add to cache
            T target = new T();
            target.InjectFrom(new NHUnproxifyInjection(), source);

            if (! cache.ContainsKey(source))
                cache.Add(source, target);

            // Ensures that there is only one instance, even after the reentrancy issues?
            return cache[source];;


        }

        private static T Transform<S,T>(S source, IDictionary<S, T> cache) where T : new()
        {
            /// TODO: Solve the issue that this function is called several times on what is probably the same thread... Which may cause unnecessary object creation, and with NHLinq calling this several times per object this might be a problem

            // Already cached?
            if (cache.ContainsKey(source))
                return cache[source];

            // Not cached, create and add to cache
            T target = new T();
            target.InjectFrom(new NHUnproxifyInjection(), source);

            if (!cache.ContainsKey(source))
                cache.Add(source, target);

            // Ensures that there is only one instance, even after the reentrancy issues?
            return cache[source]; ;


        }
    }
}
