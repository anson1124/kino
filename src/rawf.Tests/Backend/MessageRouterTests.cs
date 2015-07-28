﻿using System;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;
using rawf.Backend;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Frontend;
using rawf.Messaging;
using rawf.Tests.Backend.Setup;

namespace rawf.Tests.Backend
{
    [TestFixture]
    public class MessageRouterTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);

        [Test]
        public void TestMessageRouterUponStart_CreatesRouterLocalAndScaleoutSockets()
        {
            var connectivityProvider = new Mock<IConnectivityProvider>();
            connectivityProvider.Setup(m => m.CreateRouterSocket()).Returns(new StubSocket());
            connectivityProvider.Setup(m => m.CreateScaleOutBackendSocket()).Returns(new StubSocket());
            connectivityProvider.Setup(m => m.CreateScaleOutFrontendSocket()).Returns(new StubSocket());
            var messageHandlerStack = new RoutingTable();
            var router = new MessageRouter(connectivityProvider.Object, messageHandlerStack);
            router.Start();

            connectivityProvider.Verify(m => m.CreateRouterSocket(), Times.Once);
            connectivityProvider.Verify(m => m.CreateScaleOutBackendSocket(), Times.Once);
            connectivityProvider.Verify(m => m.CreateScaleOutFrontendSocket(), Times.Once);
        }

        [Test]
        public void TestRegisterMessageHandlers_AddsActorIdentifier()
        {
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateRouterSocket()).Returns(socket);
            connectivityProvider.Setup(m => m.CreateScaleOutBackendSocket()).Returns(new StubSocket());
            connectivityProvider.Setup(m => m.CreateScaleOutFrontendSocket()).Returns(new StubSocket());

            var messageHandlerStack = new RoutingTable();
            var router = new MessageRouter(connectivityProvider.Object, messageHandlerStack);
            router.Start();

            var messageIdentity = Guid.NewGuid().ToByteArray();
            var version = Guid.NewGuid().ToByteArray();
            var socketIdentity = Guid.NewGuid().ToByteArray();
            var message = Message.Create(new RegisterMessageHandlers
                                         {
                                             SocketIdentity = socketIdentity,
                                             Registrations = new[]
                                                             {
                                                                 new MessageHandlerRegistration
                                                                 {
                                                                     Identity = messageIdentity,
                                                                     Version = version,
                                                                     IdentityType = IdentityType.Actor
                                                                 }
                                                             }
                                         }, RegisterMessageHandlers.MessageIdentity);
            socket.DeliverMessage(message);

            Thread.Sleep(AsyncOp);

            var identifier = messageHandlerStack.Pop(new MessageHandlerIdentifier(version, messageIdentity));

            Assert.IsNotNull(identifier);
            Assert.IsTrue(identifier.Equals(new SocketIdentifier(socketIdentity)));
            CollectionAssert.AreEqual(socketIdentity, identifier.SocketId);
        }

        [Test]
        public void TestHandlerForReceiverIdentifier_HasHighestPriority()
        {
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var routerSocket = new StubSocket();
            var callbackSocketIdentity = new SocketIdentifier(Guid.NewGuid().ToString().GetBytes());
            var callbackIdentifier = new MessageHandlerIdentifier(Message.CurrentVersion, callbackSocketIdentity.SocketId);

            connectivityProvider.Setup(m => m.CreateRouterSocket()).Returns(routerSocket);
            connectivityProvider.Setup(m => m.CreateScaleOutBackendSocket()).Returns(new StubSocket());
            connectivityProvider.Setup(m => m.CreateScaleOutFrontendSocket()).Returns(new StubSocket());

            var messageHandlerStack = new Mock<IRoutingTable>();
            messageHandlerStack.Setup(m => m.Pop(It.Is<MessageHandlerIdentifier>(mhi => mhi.Equals(callbackIdentifier))))
                               .Returns(callbackSocketIdentity);

            var router = new MessageRouter(connectivityProvider.Object, messageHandlerStack.Object);
            router.Start();

            var message = SendMessageOverMessageHub(callbackSocketIdentity);

            routerSocket.DeliverMessage(message);

            Thread.Sleep(AsyncOp);

            messageHandlerStack.Verify(m => m.Pop(It.Is<MessageHandlerIdentifier>(mhi => mhi.Equals(callbackIdentifier))), Times.Once());
        }

        [Test]
        public void TestMessageIsRouted_BasedOnHandlerIdentities()
        {
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var routerSocket = new StubSocket();
            var actorSocketIdentity = new SocketIdentifier(Guid.NewGuid().ToString().GetBytes());
            var actorIdentifier = new MessageHandlerIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);

            connectivityProvider.Setup(m => m.CreateRouterSocket()).Returns(routerSocket);
            connectivityProvider.Setup(m => m.CreateScaleOutBackendSocket()).Returns(new StubSocket());
            connectivityProvider.Setup(m => m.CreateScaleOutFrontendSocket()).Returns(new StubSocket());

            var messageHandlerStack = new Mock<IRoutingTable>();
            messageHandlerStack.Setup(m => m.Pop(It.Is<MessageHandlerIdentifier>(mhi => mhi.Equals(actorIdentifier))))
                               .Returns(actorSocketIdentity);

            var router = new MessageRouter(connectivityProvider.Object, messageHandlerStack.Object);
            router.Start();

            var message = Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
            routerSocket.DeliverMessage(message);

            Thread.Sleep(AsyncOp);

            messageHandlerStack.Verify(m => m.Pop(It.Is<MessageHandlerIdentifier>(mhi => mhi.Equals(actorIdentifier))), Times.Once());
        }

        [Test]
        public void TestIfLocalRoutingTableHasNoMessageHandlerRegistration_MessageRoutedToOtherNodes()
        {
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var routerSocket = new StubSocket();
            var scaleOutBackEndSocket = new StubSocket();
            scaleOutBackEndSocket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            connectivityProvider.Setup(m => m.CreateRouterSocket()).Returns(routerSocket);
            connectivityProvider.Setup(m => m.CreateScaleOutBackendSocket()).Returns(scaleOutBackEndSocket);
            connectivityProvider.Setup(m => m.CreateScaleOutFrontendSocket()).Returns(new StubSocket());

            var messageHandlerStack = new Mock<IRoutingTable>();

            var router = new MessageRouter(connectivityProvider.Object, messageHandlerStack.Object);
            router.Start();

            var message = Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
            routerSocket.DeliverMessage(message);

            Thread.Sleep(AsyncOp);

            var messageOut = scaleOutBackEndSocket.GetSentMessages().Last();

            Assert.AreEqual(message, messageOut);
        }

        [Test]
        public void TestMessageReceivedFromOtherNode_ForwardedToLocalRouterSocket()
        {
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var routerSocket = new StubSocket();
            var scaleOutFrontEndSocket = new StubSocket();
            scaleOutFrontEndSocket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            connectivityProvider.Setup(m => m.CreateRouterSocket()).Returns(routerSocket);
            connectivityProvider.Setup(m => m.CreateScaleOutBackendSocket()).Returns(new StubSocket());
            connectivityProvider.Setup(m => m.CreateScaleOutFrontendSocket()).Returns(scaleOutFrontEndSocket);

            var messageHandlerStack = new Mock<IRoutingTable>();

            var router = new MessageRouter(connectivityProvider.Object, messageHandlerStack.Object);
            router.Start();

            var message = Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
            scaleOutFrontEndSocket.DeliverMessage(message);

            Thread.Sleep(AsyncOp);

            var messageOut = scaleOutFrontEndSocket.GetSentMessages().Last();

            Assert.AreEqual(message, messageOut);
        }

        private static IMessage SendMessageOverMessageHub(SocketIdentifier callbackSocketIdentifier)
        {
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var clientSendingSocket = new StubSocket();
            var clientReceivingSocket = new StubSocket();
            clientReceivingSocket.SetIdentity(callbackSocketIdentifier.SocketId);
            connectivityProvider.Setup(m => m.CreateMessageHubSendingSocket()).Returns(clientSendingSocket);
            connectivityProvider.Setup(m => m.CreateMessageHubReceivingSocket()).Returns(clientReceivingSocket);

            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

            var messageHub = new MessageHub(connectivityProvider.Object, new CallbackHandlerStack());
            messageHub.Start();
            messageHub.EnqueueRequest(message, callback);
            Thread.Sleep(AsyncOp);

            return clientSendingSocket.GetSentMessages().Last();
        }
    }
}