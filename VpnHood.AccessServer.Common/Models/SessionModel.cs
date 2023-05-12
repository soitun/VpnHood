﻿using VpnHood.Common.Messaging;

namespace VpnHood.AccessServer.Models;

public class SessionModel
{
    public long SessionId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid AccessId { get; set; }
    public Guid DeviceId { get; set; }
    public string ClientVersion { get; set; } = default!;
    public string? DeviceIp { get; set; }
    public string? Country { get; set; }
    public byte[] SessionKey { get; set; } = default!;
    public Guid ServerId { get; set; }
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public SessionSuppressType SuppressedBy { get; set; }
    public SessionSuppressType SuppressedTo { get; set; }
    public SessionErrorCode ErrorCode { get; set; }
    public bool IsArchived { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ExtraData { get; set; }

    public virtual ServerModel? Server { get; set; }
    public virtual DeviceModel? Device { get; set; }
    public virtual AccessModel? Access { get; set; }

    public SessionModel Clone()
    {
        return new SessionModel
        {
            ProjectId = ProjectId,
            AccessId = AccessId,
            DeviceId = DeviceId,
            ServerId = ServerId,
            SessionId = SessionId,
            ClientVersion = ClientVersion,
            Country = Country,
            CreatedTime = CreatedTime,
            DeviceIp = DeviceIp,
            SessionKey = SessionKey,
            LastUsedTime = LastUsedTime,
            EndTime = EndTime,
            SuppressedBy = SuppressedBy,
            SuppressedTo = SuppressedTo,
            ErrorMessage = ErrorMessage,
            ErrorCode = ErrorCode,
            IsArchived = IsArchived
        };
    }
}