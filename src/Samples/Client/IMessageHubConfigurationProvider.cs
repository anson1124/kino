﻿using kino.Client;

namespace Client
{
    public interface IMessageHubConfigurationProvider
    {
        IMessageHubConfiguration GetConfiguration();
    }
}