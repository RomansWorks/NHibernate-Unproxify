using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Collection;
using NHibernate.Proxy;
using Omu.ValueInjecter;
using System.Collections;
using System.ComponentModel;
using NHibernateUnproxify.Base;

namespace NHibernateUnproxify
{
    /// <summary>
    /// A ValueInjecter injection which copies nulls instead of uninitialized proxy objects to from the source to the target. Works well with object trees, including circular references.
    /// </summary>
    public class NHUnproxifyInjection : SmartConventionInjection
    {
        private class LazySetter
        {
            public bool IsListSetter;

            public LazySetter(object target_obj, PropertyDescriptor target_prop)
            {
                this.target_obj = target_obj;
                this.target_prop = target_prop;
                IsListSetter = false;
            }

            public LazySetter(IList list)
            {
                this.target_list = list;
                IsListSetter = true;
            }

            public object target_obj;
            public PropertyDescriptor target_prop;
            public IList target_list;
        }

        private IDictionary<object, object> resolvedObjects = new Dictionary<object,object>();
        private IDictionary<object, IList<LazySetter>> lazySetters = new Dictionary<object, IList<LazySetter>>();

        protected override bool Match(SmartConventionInfo c)
        {
            var name_match = (c.SourceProp.Name == c.TargetProp.Name);

            return name_match;
        }

        protected override object SetValue(ref bool setValue, SmartValueInfo info)
        {
            //return base.SetValue(ref setValue, info);

            var source_value = info.SourcePropValue;

            if (CheckIsUnitializedProxy(source_value))
                return null;
                     

            // Nulls don't have to be taken cared of
            if (source_value == null)
                return null;

            // If we already have the object completely worked out, return the previous result. 
            if (CheckIfAlreadyResolved(source_value))
               return GetAlreadyResolved(source_value);

            // If the object is currently being worked out, return null in the meanwhile and put your trust in the lazy setters
            if (CheckIfCurrentlyResolving(source_value))
            {
                RequestForLazySetting(source_value, info.Target, info.TargetProp);
                return null;
            }

            SetCurrentlyResolving(source_value);
                
            // Collections do not appear as proxies, even if they are not initialized. My convention is to return null here.
            var source_value_type = source_value.GetType();
            if (source_value_type.GetInterfaces().Contains(typeof(IPersistentCollection)))
            {
                var collection_result = SetValueForPersistentCollection(info);
                SetAlreadyResolved(source_value, collection_result);
                return collection_result;
            }

            // Not a proxy, collection, null or previously resolved. Resolve using regular methods:
            // NOTE: What about proxy=>regular object=>proxy? Maybe I should rebuild here too to use recursion? 

            //var simple_result = base.SetValue(ref setValue, info);
            object simple_result;
            if (source_value_type.IsPrimitive || source_value_type == typeof(string) || source_value_type == typeof(DateTime) || source_value_type == typeof(Guid))
                simple_result = base.SetValue(ref setValue, info);
            else
                simple_result = Resolve(source_value_type, source_value);
            
            SetAlreadyResolved(source_value, simple_result);
            
            return simple_result;

            
        }

        private object SetValueForPersistentCollection(SmartValueInfo info)
        {
            var persistent_collection = (IPersistentCollection)info.SourcePropValue;
            if (!persistent_collection.WasInitialized)
                return null;

            // Initialized collection, return a new and populated IList                
            var source_as_obj_list = (IList)info.SourcePropValue;
            if (source_as_obj_list.Count == 0) return null;

            var source_list_item_type = source_as_obj_list[0].GetType();
            Type list_type = typeof(List<>).MakeGenericType(source_list_item_type);
            var source_as_list = (IList)info.SourcePropValue;
            var target_as_list = (IList)Activator.CreateInstance(list_type);

            foreach (var source_item in source_as_list)
            {
                //bool setValue = true;
                if (source_item == null) continue;
                
                //var target_item = SetValue(ref setValue, info);

                // Temp: Perform pre-existance checks here too
                if (CheckIsUnitializedProxy(source_item))
                    return null;

                if (CheckIfAlreadyResolved(source_item))
                {
                    target_as_list.Add(GetAlreadyResolved(source_item));
                    continue;
                }

                if (CheckIfCurrentlyResolving(source_item))
                {
                    RequestForLazySetting(source_item, target_as_list);
                    continue;
                }


                // TODO: Change to a non-instance creating function, as this defeats the references
                var target_item = Resolve( source_item.GetType(), source_item );
                target_as_list.Add(target_item);
            }

            return target_as_list;
        }

        private bool CheckIsUnitializedProxy(object source)
        {
            // The object is a proxy. We handle it carefully without accessing the proxied object and triggering lazy loading on a an unitialized proxy. Therefore this is our first check.
            if (NHibernate.Proxy.NHibernateProxyHelper.IsProxy(source))
            {
                var proxy = source as INHibernateProxy;
                if (proxy.HibernateLazyInitializer.IsUninitialized)
                    return true;
            }

            return false;
        }

        private object Resolve(Type t, object source)
        {
            // Resolve an inner object, note that this will create a new object, and should therefore be done only at the end of all checks in SetValue
            if( t.IsArray )
                return Activator.CreateInstance(t, ((Array) source).Length).InjectFrom(this, source);
            else
                return Activator.CreateInstance(t).InjectFrom(this, source);
        }

        private bool CheckIfAlreadyResolved(object source)
        {
            return (resolvedObjects.ContainsKey(source));
        }

        private object GetAlreadyResolved(object source)
        {
            return resolvedObjects[source];
        }

        private bool CheckIfCurrentlyResolving(object source)
        {
            return lazySetters.ContainsKey(source);
        }

        private void SetCurrentlyResolving(object source)
        {
            lazySetters.Add(source, new List<LazySetter>());
        }

        private void RequestForLazySetting(object source, object target_obj, PropertyDescriptor target_prop)
        {
            var lazySetter = new LazySetter( target_obj, target_prop );
            lazySetters[source].Add(lazySetter);
        }

        private void RequestForLazySetting(object source, IList target_list)
        {
            var lazySetter = new LazySetter(target_list);
            lazySetters[source].Add(lazySetter);
        }

        private void SetAlreadyResolved(object source, object resolved_obj)
        {
            // Add to resolved list
            resolvedObjects.Add(source, resolved_obj); 

            // Time to set lazy properties
            var lazySettersToSet = lazySetters[source];
            if (lazySettersToSet == null) return;

            foreach (var lazySetter in lazySettersToSet)
            {
                if (lazySetter.IsListSetter)
                    lazySetter.target_list.Add(resolved_obj);
                else
                    lazySetter.target_prop.SetValue(lazySetter.target_obj, resolved_obj);
            }

            // Remove lazy properties collection for this object
            lazySetters[source].Clear();
            lazySetters[source] = null;

        }
    }   
}
