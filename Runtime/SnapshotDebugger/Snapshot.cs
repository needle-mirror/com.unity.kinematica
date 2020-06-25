using System;
using System.Collections.Generic;

namespace Unity.SnapshotDebugger
{
    [Serializable]
    internal sealed class Snapshot
    {
        public Identifier<Snapshot> identifier
        {
            get; private set;
        }

        internal int aggregateIdentifier;
        internal int providerIdentifier;

        public class AggregateReference
        {
            public Identifier<Aggregate> identifier;

            public class ProviderReference
            {
                public Identifier<SnapshotProvider> identifier;

                public Buffer payload;

                public Buffer customPayload;
            }

            public static AggregateReference Create(Identifier<Aggregate> identifier)
            {
                return new AggregateReference(identifier);
            }

            public void Create(SnapshotProvider provider)
            {
                var payload = Buffer.Create();

                provider.WriteToStream(payload);

                _providers.Add(new ProviderReference
                {
                    identifier = provider.identifier,
                    payload = payload
                });
            }

            public int memorySize
            {
                get
                {
                    int memorySize = 0;

                    foreach (var provider in providers)
                    {
                        memorySize += provider.payload.Size;
                    }

                    return memorySize;
                }
            }

            public IEnumerable<ProviderReference> providers
            {
                get
                {
                    foreach (var providers in _providers)
                    {
                        yield return providers;
                    }
                }
            }

            AggregateReference(Identifier<Aggregate> identifier)
            {
                this.identifier = identifier;
            }

            List<ProviderReference> _providers = new List<ProviderReference>();
        }

        List<AggregateReference> _aggregates = new List<AggregateReference>();

        public AggregateReference Find(Identifier<Aggregate> identifier)
        {
            return _aggregates.Find(
                aggregate => aggregate.identifier == identifier);
        }

        public AggregateReference this[Identifier<Aggregate> identifier]
        {
            get { return Find(identifier);}
        }

        public IEnumerable<AggregateReference> aggregates
        {
            get
            {
                foreach (var aggregate in _aggregates)
                {
                    yield return aggregate;
                }
            }
        }

        public Identifier<Aggregate>[] ToArray()
        {
            var numAggregates = _aggregates.Count;

            var result = new Identifier<Aggregate>[numAggregates];

            for (int i = 0; i < numAggregates; ++i)
            {
                result[i] = _aggregates[i].identifier;
            }

            return result;
        }

        public float startTimeInSeconds
        {
            get; private set;
        }

        public float durationInSeconds
        {
            get; private set;
        }

        public float endTimeInSeconds
        {
            get { return startTimeInSeconds + durationInSeconds; }
        }

        public bool Contains(float timeStamp)
        {
            return timeStamp >= startTimeInSeconds && timeStamp < endTimeInSeconds;
        }

        public int memorySize
        {
            get
            {
                int memorySize = 0;

                foreach (var aggregate in aggregates)
                {
                    memorySize += aggregate.memorySize;
                }

                return memorySize;
            }
        }

        public static Snapshot Create(float startTime, float deltaTime)
        {
            return new Snapshot(startTime, deltaTime);
        }

        public void PostProcess()
        {
            var registry = Debugger.registry;

            foreach (var dst in aggregates)
            {
                var aggregate = registry[dst.identifier];

                if (aggregate != null)
                {
                    foreach (var reference in dst.providers)
                    {
                        var provider = aggregate[reference.identifier];

                        if (provider.RequirePostProcess)
                        {
                            reference.customPayload = Buffer.Create();
                            provider.OnWritePostProcess(reference.customPayload);
                        }
                    }
                }
            }
        }

        Snapshot(float startTime, float deltaTime)
        {
            startTimeInSeconds = startTime;
            durationInSeconds = deltaTime;

            identifier = Identifier<Snapshot>.Create();

            var registry = Debugger.registry;

            foreach (var src in registry.aggregates)
            {
                var dst = AggregateReference.Create(src.identifier);

                foreach (var provider in src.providers)
                {
                    dst.Create(provider);
                }

                _aggregates.Add(dst);
            }
        }
    }
}
