using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Omu.ValueInjecter;

namespace NHibernateUnproxify.Base
{
    /// <summary>
    /// A fast ValueInjecter injection strategy as described in http://valueinjecter.codeplex.com/wikipage?title=SmartConventionInjection&referringTitle=Home
    /// </summary>
    public abstract class SmartConventionInjection : ValueInjection
    {
        private class Path
        {
            public Type Source { get; set; }
            public Type Target { get; set; }
            public IDictionary<string, string> Pairs { get; set; }
        }

        protected abstract bool Match(SmartConventionInfo c);

        private static readonly IList<Path> paths = new List<Path>();
        private static readonly IDictionary<Type, Type> wasLearned = new Dictionary<Type, Type>();

        private Path Learn(object source, object target)
        {
            Path path = null;
            var sourceProps = source.GetProps();
            var targetProps = target.GetProps();
            var sci = new SmartConventionInfo
            {
                SourceType = source.GetType(),
                TargetType = target.GetType()
            };

            for (var i = 0; i < sourceProps.Count; i++)
            {
                var s = sourceProps[i];
                sci.SourceProp = s;

                for (var j = 0; j < targetProps.Count; j++)
                {
                    var t = targetProps[j];
                    sci.TargetProp = t;

                    if (!Match(sci)) continue;
                    if (path == null)
                        path = new Path
                        {
                            Source = sci.SourceType,
                            Target = sci.TargetType,
                            Pairs = new Dictionary<string, string> { { sci.SourceProp.Name, sci.TargetProp.Name } }
                        };
                    else path.Pairs.Add(sci.SourceProp.Name, sci.TargetProp.Name);
                }
            }
            return path;
        }

        protected override void Inject(object source, object target)
        {
            var sourceProps = source.GetProps();
            var targetProps = target.GetProps();

            if (!wasLearned.Contains(new KeyValuePair<Type, Type>(source.GetType(), target.GetType())))
            {
                lock (wasLearned)
                {
                    if (!wasLearned.Contains(new KeyValuePair<Type, Type>(source.GetType(), target.GetType())))
                    {

                        var match = Learn(source, target);
                        wasLearned.Add(source.GetType(), target.GetType());
                        if (match != null) paths.Add(match);
                    }
                }
            }

            // TODO: Solve Collection was modified; enumeration operation may not execute.
            var path = paths.SingleOrDefault(o => o.Source == source.GetType() && o.Target == target.GetType());

            if (path == null) return;

            foreach (var pair in path.Pairs)
            {
                var sp = sourceProps.GetByName(pair.Key);
                var tp = targetProps.GetByName(pair.Value);
                var setValue = true;
                var val = SetValue(ref setValue, new SmartValueInfo { Source = source, Target = target, SourceProp = sp, TargetProp = tp, SourcePropValue = sp.GetValue(source) });
                if (setValue) tp.SetValue(target, val);
            }
        }

        protected virtual object SetValue(ref bool setValue, SmartValueInfo info)
        {
            return info.SourcePropValue;
        }
    }

    public class SmartValueInfo
    {
        public PropertyDescriptor SourceProp { get; set; }
        public PropertyDescriptor TargetProp { get; set; }
        public object Source { get; set; }
        public object Target { get; set; }
        public object SourcePropValue { get; set; }
    }

    public class SmartConventionInfo
    {
        public Type SourceType { get; set; }
        public Type TargetType { get; set; }

        public PropertyDescriptor SourceProp { get; set; }
        public PropertyDescriptor TargetProp { get; set; }
    }
}
