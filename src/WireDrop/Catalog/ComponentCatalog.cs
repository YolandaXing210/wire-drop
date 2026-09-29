using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Rhino;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using WireDrop.Ranking;

namespace WireDrop.Catalog
{
    /// <summary>
    /// A port map for every installed component. Ports are only knowable by instantiating
    /// each proxy, which costs a couple of seconds across ~1500 of them — so the work is
    /// sliced across Rhino idle ticks after load and is normally finished long before the first
    /// wire drop. <see cref="EnsureBuilt"/> finishes it synchronously if it is not, and
    /// <see cref="Refresh"/> takes in plugins that load after it.
    /// </summary>
    internal sealed class ComponentCatalog
    {
        static ComponentCatalog _instance;
        public static ComponentCatalog Instance => _instance ??= new ComponentCatalog();

        readonly List<ComponentEntry> _entries = new List<ComponentEntry>();
        readonly Dictionary<string, int> _popularity =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        List<IGH_ObjectProxy> _queue;
        int _cursor;
        bool _subscribed;

        /// <summary>
        /// Every proxy already taken in, whether or not it made an entry — one that threw
        /// or had no ports is not tried again each time the panel opens.
        /// </summary>
        readonly HashSet<Guid> _seen = new HashSet<Guid>();

        public bool Ready { get; private set; }
        public int Count => _entries.Count;
        public IReadOnlyList<ComponentEntry> Entries => _entries;

        ComponentCatalog()
        {
            for (int i = 0; i < Popularity.Names.Length; i++)
                _popularity[Popularity.Names[i]] = i;
        }

        public void BeginBuild() => Refresh();

        /// <summary>
        /// Brings the catalog level with what Grasshopper has loaded now. Most plugins
        /// arrive after WireDrop does, many long after Grasshopper opened, and a user object
        /// can be saved at any time — so the library is compared again whenever a GHA
        /// loads and whenever the panel opens. Only proxies not seen before are queued,
        /// and they are built the same way as the first pass, a slice per idle tick.
        /// Anything Grasshopper has let go of since leaves the catalog too.
        /// </summary>
        public void Refresh()
        {
            try
            {
                if (!Instances.IsComponentServer) return;
                var proxies = Instances.ComponentServer.ObjectProxies?.ToList();
                if (proxies == null) return;

                var current = new HashSet<Guid>();
                var fresh = new List<IGH_ObjectProxy>();
                foreach (var proxy in proxies)
                {
                    if (proxy == null) continue;
                    current.Add(proxy.Guid);
                    if (_seen.Add(proxy.Guid)) fresh.Add(proxy);
                }

                if (_seen.Count > current.Count)
                {
                    _seen.IntersectWith(current);
                    _entries.RemoveAll(entry => !current.Contains(entry.Id));
                }

                if (fresh.Count == 0) return;

                // A build still under way takes the newcomers on at the end of its queue.
                if (_queue != null) _queue.AddRange(fresh);
                else { _queue = fresh; _cursor = 0; }

                Ready = false;
                if (!_subscribed) { RhinoApp.Idle += OnIdle; _subscribed = true; }
            }
            catch (Exception ex) { Log.Error("catalog-refresh", ex); }
        }

        void OnIdle(object sender, EventArgs e) => Slice(80);

        /// <summary>
        /// Blocks only if a drop beat the idle build to the finish, and then for no longer
        /// than <see cref="Deadline.LimitMs"/>. What is left is still built on idle: the
        /// panel opens on the part of the library that is ready, and the next one on more.
        /// Small slices, so the deadline is looked at often; one slow constructor still
        /// runs to its end, since nothing on this thread can be stopped from outside.
        /// </summary>
        public void EnsureBuilt()
        {
            if (Ready) return;
            var deadline = Deadline.After();
            while (!Ready)
            {
                if (deadline.Passed)
                {
                    Log.Once("catalog-deadline",
                        "the component library took more than " + Deadline.LimitMs / 1000
                        + " seconds to read, so the panel opened on part of it. The rest is "
                        + "read while Rhino is idle.");
                    return;
                }
                Slice(20);
            }
        }

        void Slice(int count)
        {
            if (Ready) return;
            if (_queue == null) { Finish(); return; }
            var end = Math.Min(_cursor + count, _queue.Count);
            for (; _cursor < end; _cursor++) Add(_queue[_cursor]);
            if (_cursor >= _queue.Count) Finish();
        }

        void Finish()
        {
            if (_subscribed) { RhinoApp.Idle -= OnIdle; _subscribed = false; }
            _queue = null;
            Ready = true;
        }

        void Add(IGH_ObjectProxy proxy)
        {
            // Third-party components can and do throw from their constructors; one bad
            // component must not cost us the rest of the library.
            try
            {
                if (proxy == null) return;

                // Obsolete and hidden objects are kept rather than dropped, so that Tab can
                // mean everything. They are marked, and stay out of the list until asked for.
                var hidden = proxy.Obsolete || proxy.Exposure == GH_Exposure.hidden;

                var desc = proxy.Desc;
                if (desc == null || string.IsNullOrWhiteSpace(desc.Name)) return;

                var instance = proxy.CreateInstance();
                if (instance == null) return;

                PortSpec[] inputs, outputs;
                var takesWire = true;
                if (instance is IGH_Component component)
                {
                    inputs = Describe(component.Params.Input);
                    outputs = Describe(component.Params.Output);
                }
                else if (instance is IGH_Param param)
                {
                    var self = Describe(param, 0);
                    inputs = new[] { self };
                    outputs = new[] { self };
                    takesWire = HasInputGrip(param);
                }
                else return;

                if (inputs.Length == 0 && outputs.Length == 0) return;

                _entries.Add(new ComponentEntry
                {
                    Id = proxy.Guid,
                    Name = desc.Name,
                    NickName = desc.NickName,
                    Category = string.IsNullOrEmpty(desc.Category) ? "Other" : desc.Category,
                    SubCategory = desc.SubCategory,
                    Description = desc.Description,
                    Icon = proxy.Icon,
                    Exposure = proxy.Exposure,
                    Obscure = ((int)proxy.Exposure & (int)GH_Exposure.obscure) != 0,
                    Hidden = hidden,
                    Inputs = inputs,
                    Outputs = outputs,
                    TakesWire = takesWire,
                    Popularity = _popularity.TryGetValue(desc.Name, out var p) ? p : int.MaxValue,
                });
            }
            catch { /* skip this proxy */ }
        }

        /// <summary>
        /// Whether a standalone parameter can be wired into at all. Asking its attributes is
        /// the only honest answer — the list of objects that make their own value is not ours
        /// to keep, and a third-party one arrives already knowing. Laying out an attribute
        /// costs little and the catalog is built a slice at a time on idle anyway.
        ///
        /// False when we cannot tell: refusing to splice through something is a small loss,
        /// and splicing through something that ignores the wire is a broken definition.
        /// </summary>
        static bool HasInputGrip(IGH_Param param)
        {
            try
            {
                if (param.Attributes == null) param.CreateAttributes();
                return param.Attributes?.HasInputGrip ?? false;
            }
            catch { return false; }
        }

        static PortSpec[] Describe(IList<IGH_Param> parameters)
        {
            if (parameters == null) return Array.Empty<PortSpec>();
            var list = new List<PortSpec>(parameters.Count);
            for (int i = 0; i < parameters.Count; i++)
            {
                var spec = Describe(parameters[i], i);
                if (spec != null) list.Add(spec);
            }
            return list.ToArray();
        }

        static PortSpec Describe(IGH_Param param, int index)
        {
            if (param == null) return null;
            Type goo = null;
            try { goo = param.Type; } catch { }
            var shortName = TypeCompat.ShortName(goo);

            // Asked of the type itself rather than looked up in a list of names we happen
            // to know, so a third-party geometry type is understood the first time it is seen.
            var isGeometric = goo != null && typeof(IGH_GeometricGoo).IsAssignableFrom(goo);
            if (isGeometric) TypeCompat.RegisterGeometric(shortName);

            return new PortSpec
            {
                Index = index,
                Name = string.IsNullOrEmpty(param.Name) ? shortName : param.Name,
                NickName = string.IsNullOrEmpty(param.NickName) ? shortName : param.NickName,
                Description = param.Description,
                GooType = goo,
                TypeName = shortName,
                IsGeneric = string.Equals(shortName, "Generic", StringComparison.Ordinal),
                IsGeometric = isGeometric,
            };
        }
    }
}
