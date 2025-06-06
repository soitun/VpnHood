﻿using System.Net;
using VpnHood.Core.Common.Exceptions;
using VpnHood.Core.Common.Messaging;
using VpnHood.Core.Toolkit.Utils;

namespace VpnHood.Core.Client.Exceptions;

public class RedirectHostException(SessionResponse sessionResponse)
    : SessionException(sessionResponse)
{
    public IPEndPoint[] RedirectHostEndPoints {
        get {
            if (!VhUtils.IsNullOrEmpty(SessionResponse.RedirectHostEndPoints))
                return SessionResponse.RedirectHostEndPoints;

            return SessionResponse.RedirectHostEndPoint != null
                ? [SessionResponse.RedirectHostEndPoint]
                : [];
        }
    }
}