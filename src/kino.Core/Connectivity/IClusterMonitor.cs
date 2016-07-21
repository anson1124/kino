﻿using System;
using System.Collections.Generic;

namespace kino.Core.Connectivity
{
    public interface IClusterMonitor
    {
        bool Start(TimeSpan startTimeout);

        void Stop();

        void RegisterSelf(IEnumerable<MessageIdentifier> messageHandlers, string securityDomain);

        void RequestClusterRoutes();

        void UnregisterSelf(IEnumerable<MessageIdentifier> messageIdentifiers);

        IEnumerable<SocketEndpoint> GetClusterMembers();

        void DiscoverMessageRoute(MessageIdentifier messageIdentifier);
    }
}