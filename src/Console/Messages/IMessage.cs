﻿using System;

namespace Console.Messages
{
    public interface IMessage
    {
        void RegisterCallbackPoint(ICallbackPoint callbackPoint);
        T GetPayload<T>() where T : IPayload;

        DistributionPattern Distribution { get; }

        string Version { get; }
        string Identity { get; }
        byte[] CorrelationId { get; }
        byte[] ReceiverIdentity { get; }

        byte[] CallbackIdentity { get; }
        byte[] CallbackReceiverIdentity { get; }

        TimeSpan TTL { get; set; }
        byte[] Body { get; }
        
    }

    public enum DistributionPattern
    {
        Unicast = 0,
        Broadcast = 1
    }
}