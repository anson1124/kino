﻿using System;

namespace kino.Core.Connectivity
{
    public class RendezvousEndpoint : IEquatable<RendezvousEndpoint>
    {
        private readonly int hashCode;

        public RendezvousEndpoint(string unicastUri, string broadcastUri)
            : this(new Uri(unicastUri), new Uri(broadcastUri))
        {
        }

        public RendezvousEndpoint(Uri unicastUri, Uri broadcastUri)
        {
            BroadcastUri = broadcastUri;
            UnicastUri = unicastUri;

            hashCode = CalculateHashCode();
        }

        public bool Equals(RendezvousEndpoint other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(BroadcastUri, other.BroadcastUri) && Equals(UnicastUri, other.UnicastUri);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((RendezvousEndpoint) obj);
        }

        public override int GetHashCode()
            => hashCode;

        private int CalculateHashCode()
        {
            unchecked
            {
                return ((BroadcastUri?.GetHashCode() ?? 0) * 397) ^ (UnicastUri?.GetHashCode() ?? 0);
            }
        }

        public Uri BroadcastUri { get; }

        public Uri UnicastUri { get; }
    }
}