﻿namespace VpnHood.AccessServer.Models;

public class AccessTokenModel
{
    public required Guid ProjectId { get; init; }
    public required Guid AccessTokenId { get; init; }
    public required string? AccessTokenName { get; set; }
    public required int SupportCode { get; set; }
    public required byte[] Secret { get; set; }
    public required Guid ServerFarmId { get; set; }
    public required long MaxTraffic { get; set; }
    public required int MaxDevice { get; set; }
    public required string? Url { get; set; }
    public required bool IsPublic { get; set; }
    public required bool IsEnabled { get; set; }
    public required int Lifetime { get; set; }
    public required DateTime? ExpirationTime { get; set; }
    public required DateTime? FirstUsedTime { get; set; }
    public required DateTime? LastUsedTime { get; set; }
    public required DateTime CreatedTime { get; init; }
    public required DateTime ModifiedTime { get; set; }
    public required bool IsDeleted { get; set; }

    public virtual ProjectModel? Project { get; set; }
    public virtual ServerFarmModel? ServerFarm { get; set; }
}