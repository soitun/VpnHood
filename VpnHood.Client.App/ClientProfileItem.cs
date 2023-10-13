﻿using System;
using VpnHood.Common;

namespace VpnHood.Client.App;

public class ClientProfileItem
{
    public ClientProfileItem(ClientProfile clientProfile, Token token)
    {
        ClientProfile = clientProfile;
        Token = token;
    }

    public Guid ClientProfileId => ClientProfile.ClientProfileId;
    public ClientProfile ClientProfile { get; set; }
    public Token Token { get; set; }
}