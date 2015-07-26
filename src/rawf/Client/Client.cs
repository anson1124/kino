﻿using rawf.Messaging;

namespace rawf.Client
{
    // TODO: This class is not really needed.
    // Think of removing it and reorganizing the whole Client namespace
    public class Client
    {
        private readonly MessageHub messageHub;

        public Client(MessageHub messageHub)
        {
            this.messageHub = messageHub;
        }

        public IPromise Send(IMessage message, ICallbackPoint callbackPoint)
        {
            return messageHub.EnqueueRequest(message, callbackPoint);
        }
    }
}