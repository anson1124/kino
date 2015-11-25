﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using kino.Client;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Client
{
    [TestFixture]
    public class MessagHubTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(2);
        private MessageHubSocketFactory messageHubSocketFactory;
        private readonly string localhost = "tcp://localhost:43";
        private Mock<ISocketFactory> socketFactory;
        private MessageHubConfiguration config;
        private ILogger logger;
        private Mock<ICallbackHandlerStack> callbackHandlerStack;

        [SetUp]
        public void Setup()
        {
            callbackHandlerStack = new Mock<ICallbackHandlerStack>();
            logger = new Mock<ILogger>().Object;
            messageHubSocketFactory = new MessageHubSocketFactory();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(messageHubSocketFactory.CreateSocket);
            config = new MessageHubConfiguration
                     {
                         RouterUri = new Uri(localhost)
                     };
        }

        [Test]
        public void TestOnMessageHubStart_RegisterationMessageIsSent()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            logger);
            try
            {
                messageHub.Start();

                var sendingSocket = messageHubSocketFactory.GetSendingSocket();
                var receivingSocket = messageHubSocketFactory.GetReceivingSocket();
                var message = sendingSocket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.IsNotNull(message);
                var registration = message.GetPayload<RegisterInternalMessageRouteMessage>();
                CollectionAssert.AreEqual(receivingSocket.GetIdentity(), registration.SocketIdentity);
                var handler = registration.MessageContracts.First();
                CollectionAssert.AreEqual(IdentityExtensions.Empty, handler.Version);
                CollectionAssert.AreEqual(receivingSocket.GetIdentity(), handler.Identity);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestEnqueueRequest_RegistersMessageAndExceptionHandlers()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                messageHub.EnqueueRequest(message, callback);

                Thread.Sleep(AsyncOp);

                callbackHandlerStack.Verify(m => m.Push(It.Is<CorrelationId>(c => Unsafe.Equals(c.Value, message.CorrelationId)),
                                                        It.IsAny<IPromise>(),
                                                        It.Is<IEnumerable<MessageIdentifier>>(en => ContainsMessageAndExceptionRegistrations(en))),
                                            Times.Once);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestEnqueueRequest_SendsMessageWithCallbackReceiverIdentityEqualsToReceivingSocketIdentity()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                messageHub.EnqueueRequest(message, callback);

                Thread.Sleep(AsyncOp);

                var messageOut = messageHubSocketFactory.GetSendingSocket().GetSentMessages().Last();
                var receivingSocket = messageHubSocketFactory.GetReceivingSocket();

                Assert.IsNotNull(messageOut);
                CollectionAssert.AreEqual(receivingSocket.GetIdentity(), messageOut.CallbackReceiverIdentity);
                CollectionAssert.AreEqual(callback.MessageIdentifiers, messageOut.CallbackPoint);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestWhenMessageReceived_CorrespondingPromiseResultSet()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                var promise = messageHub.EnqueueRequest(message, callback);
                callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns(promise);
                messageHubSocketFactory.GetReceivingSocket().DeliverMessage(message);

                var response = promise.GetResponse().Result;

                Assert.IsNotNull(response);
                Assert.AreEqual(message, response);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestWhenPromiseIsDisposed_ItsCallbackIsRemoved()
        {
            var callbackHandlerStack = new CallbackHandlerStack();
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack,
                                            config,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                var promise = messageHub.EnqueueRequest(message, callback);
                Thread.Sleep(AsyncOpCompletionDelay);

                messageHubSocketFactory.GetReceivingSocket().DeliverMessage(message);

                Assert.IsNull(callbackHandlerStack.Pop(new CallbackHandlerKey
                                                       {
                                                           Version = callback.MessageIdentifiers.Single().Version,
                                                           Identity = callback.MessageIdentifiers.Single().Identity,
                                                           Correlation = promise.CorrelationId.Value
                                                       }));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestWhenPromiseResultIsSet_ItsCallbackIsRemoved()
        {
            var callbackHandlerStack = new CallbackHandlerStack();
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack,
                                            config,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                var promise = messageHub.EnqueueRequest(message, callback);
                Thread.Sleep(AsyncOpCompletionDelay);

                promise.Dispose();

                Assert.IsNull(callbackHandlerStack.Pop(new CallbackHandlerKey
                                                       {
                                                           Version = callback.MessageIdentifiers.Single().Version,
                                                           Identity = callback.MessageIdentifiers.Single().Identity,
                                                           Correlation = promise.CorrelationId.Value
                                                       }));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestWhenExceptionMessageReceived_PromiseThrowsException()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                var promise = messageHub.EnqueueRequest(message, callback);
                callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns(promise);
                var errorMessage = Guid.NewGuid().ToString();
                var exception = Message.Create(new ExceptionMessage {Exception = new Exception(errorMessage)});
                messageHubSocketFactory.GetReceivingSocket().DeliverMessage(exception);

                Assert.Throws<AggregateException>(() => { var response = promise.GetResponse().Result; }, errorMessage);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestWhenMessageReceivedAndNoHandlerRegistered_PromiseIsNotResolved()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                var promise = messageHub.EnqueueRequest(message, callback);
                callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns((IPromise) null);
                messageHubSocketFactory.GetReceivingSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                Assert.IsFalse(promise.GetResponse().Wait(AsyncOpCompletionDelay));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        private bool ContainsMessageAndExceptionRegistrations(IEnumerable<MessageIdentifier> registrations)
        {
            return registrations.Any(h => Unsafe.Equals(h.Identity, MessageIdentifier.Create<SimpleMessage>().Identity))
                   && registrations.Any(h => Unsafe.Equals(h.Identity, KinoMessages.Exception.Identity));
        }
    }
}