using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Rhino;
using Grasshopper.Kernel;
using WireDrop.Ranking;

namespace WireDrop.Catalog
{
    /// <summary>
    /// A port map for every installed component. Ports are only knowable by instantiating
    /// each proxy, which costs a couple of seconds across ~1500 of them — so the work is
    /// sliced across Rhino idle ticks after load and is normally finished long before the first
    /// wire drop. <see cref="EnsureBuilt"/> finishes it synchronously if it is not.
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

        public bool Ready { get; private set; }
        public int Count => _entries.Count;
        public IReadOnlyList<ComponentEntry> Entries => _entries;

        ComponentCatalog()
        {
            for (int i = 0; i < Popularity.Names.Length; i++)
                _popularity[Popularity.Names[i]] = i;
        }

        public void BeginBuild()
        {
            if (Ready || _subscribed) return;
            try
            {
                if (!Instances.IsComponentServer) return;
                _queue = Instances.ComponentServer.ObjectProxies?.ToList();
                if (_queue == null || _queue.Count == 0) return;
                _cursor = 0;
                RhinoApp.Idle += OnIdle;
                _subscribed = true;
            }
            catch (Exception ex) { Log.Error("catalog-begin", ex); }
        }

        void OnIdle(object sender, EventArgs e) => Slice(80);

        /// <summary>Blocks only if a drop beat the idle build to the finish.</summary>
        public void EnsureBuilt()
        {
            if (Ready) return;
            if (!_subscribed) BeginBuild();
            var guard = 0;
            while (!Ready && guard++ < 100000) Slice(400);
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
                if (proxy == null || proxy.Obsolete) return;
                if (proxy.Exposure == GH_Exposure.hidden) return;
                var desc = proxy.Desc;
                if (desc == null || string.IsNullOrWhiteSpace(desc.Name)) return;

                var instance = proxy.CreateInstance();
                if (instance == null) return;

                PortSpec[] inputs, outputs;
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
                    Icon = proxy.Icon,
                    Exposure = proxy.Exposure,
                    Obscure = ((int)proxy.Exposure & (int)GH_Exposure.obscure) != 0,
                    Inputs = inputs,
                    Outputs = outputs,
                    Popularity = _popularity.TryGetValue(desc.Name, out var p) ? p : int.MaxValue,
                });
            }
            catch { /* skip this proxy */ }
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
            return new PortSpec
            {
                Index = index,
                Name = string.IsNullOrEmpty(param.Name) ? shortName : param.Name,
                NickName = string.IsNullOrEmpty(param.NickName) ? shortName : param.NickName,
                GooType = goo,
                TypeName = shortName,
                IsGeneric = string.Equals(shortName, "Generic", StringComparison.Ordinal),
            };
        }
    }
}
