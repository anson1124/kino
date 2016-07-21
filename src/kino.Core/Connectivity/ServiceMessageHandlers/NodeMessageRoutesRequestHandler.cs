using System.Linq;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class NodeMessageRoutesRequestHandler : IServiceMessageHandler
    {
        private readonly IClusterMonitor clusterMonitor;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ISecurityProvider securityProvider;

        public NodeMessageRoutesRequestHandler(IClusterMonitorProvider clusterMonitorProvider,
                                               IInternalRoutingTable internalRoutingTable,
                                               ISecurityProvider securityProvider)
        {
            clusterMonitor = clusterMonitorProvider.GetClusterMonitor();
            this.internalRoutingTable = internalRoutingTable;
            this.securityProvider = securityProvider;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsMessageRoutesRequest(message);
            if (shouldHandle)
            {
                if (securityProvider.SecurityDomainIsAllowed(message.SecurityDomain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var messageIdentifiers = internalRoutingTable.GetMessageIdentifiers()
                                                                 .Where(mi => securityProvider.GetSecurityDomain(mi.Identity) == message.SecurityDomain)
                                                                 .ToList();
                    if (messageIdentifiers.Any())
                    {
                        clusterMonitor.RegisterSelf(messageIdentifiers, message.SecurityDomain);
                    }
                }
            }

            return shouldHandle;
        }

        private static bool IsMessageRoutesRequest(IMessage message)
            => message.Equals(KinoMessages.RequestNodeMessageRoutes);
    }
}