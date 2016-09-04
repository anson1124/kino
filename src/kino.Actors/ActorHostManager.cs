﻿using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Core.Security;
using kino.Core.Sockets;

namespace kino.Actors
{
    public class ActorHostManager : IActorHostManager
    {
        private readonly ISocketFactory socketFactory;
        private readonly IRouterConfigurationProvider routerConfigurationProvider;
        private readonly ISecurityProvider securityProvider;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ILogger logger;
        private readonly IList<IActorHost> actorHosts;
        private readonly object @lock = new object();
        private bool isDisposed = false;

        public ActorHostManager(ISocketFactory socketFactory,
                                IRouterConfigurationProvider routerConfigurationProvider,
                                ISecurityProvider securityProvider,
                                IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.routerConfigurationProvider = routerConfigurationProvider;
            this.securityProvider = securityProvider;
            this.performanceCounterManager = performanceCounterManager;
            this.logger = logger;
            actorHosts = new List<IActorHost>();
        }

        public void AssignActor(IActor actor, ActorHostInstancePolicy actorHostInstancePolicy = ActorHostInstancePolicy.TryReuseExisting)
        {
            AssertNotDisposed();

            lock (@lock)
            {
                GetOrCreateActorHost(actor, actorHostInstancePolicy).AssignActor(actor);
            }
        }

        private void AssertNotDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private IActorHost GetOrCreateActorHost(IActor actor, ActorHostInstancePolicy actorHostInstancePolicy)
        {
            var actorHost = actorHosts.FirstOrDefault(ah => ah.CanAssignActor(actor));

            if (actorHostInstancePolicy == ActorHostInstancePolicy.AlwaysCreateNew
                || !actorHosts.Any()
                || actorHost == null)
            {
                var routerConfiguration = routerConfigurationProvider.GetRouterConfiguration();
                actorHost = new ActorHost(socketFactory,
                                          new ActorHandlerMap(),
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider,
                                          performanceCounterManager,
                                          logger);
                actorHost.Start();
                actorHosts.Add(actorHost);
            }

            return actorHost;
        }

        public void Dispose()
        {
            try
            {
                actorHosts.ForEach(actorHost => actorHost.Stop());
                actorHosts.Clear();

                isDisposed = true;
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }
    }
}