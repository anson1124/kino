using System;
using System.Collections.Generic;
using kino.Core;

namespace kino.Consensus.Configuration
{
    public class SynodConfiguration : ISynodConfiguration
    {
        private readonly HashSet<Uri> synod;

        public SynodConfiguration(ISynodConfigurationProvider configProvider)
        {
            LocalNode = new Node(configProvider.LocalNode, ReceiverIdentifier.CreateIdentity());
            synod = new HashSet<Uri>(configProvider.Synod);
        }

        public Node LocalNode { get; }

        public IEnumerable<Uri> Synod => synod;

        public bool BelongsToSynod(Uri node)
            => synod.Contains(node);
    }
}