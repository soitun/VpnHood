﻿namespace VpnHood.AccessServer.Models;

public class ServerModel
{
    public Guid ProjectId { get; set; }
    public Guid ServerId { get; set; }
    public string ServerName { get; set; } = default!;
    public string? Version { get; set; } 
    public string? EnvironmentVersion { get; set; }
    public string? OsInfo { get; set; }
    public string? MachineName { get; set; }
    public long? TotalMemory { get; set; }
    public int? LogicalCoreCount { get; set; }
    public DateTime? ConfigureTime { get; set; }
    public DateTime CreatedTime { get; set; }
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    public Guid AuthorizationCode { get; set; }
    public byte[] ManagementSecret { get; set; } = default!;
    public Guid ServerFarmId { get; set; }
    public Guid ConfigCode { get; set; }
    public Guid? LastConfigCode { get; set; }
    public string? LastConfigError { get; set; }
    public bool AutoConfigure { get; set; }
    public bool IsDeleted { get; set; } = false;
    public List<AccessPointModel> AccessPoints { get; set; } = [];

    public virtual ProjectModel? Project { get; set; }
    public virtual ServerFarmModel? ServerFarm { get; set; }
    public virtual ServerStatusModel? ServerStatus { get; set; } // ignored
    public virtual ICollection<SessionModel>? Sessions { get; set; }
    public virtual ICollection<ServerStatusModel>? ServerStatuses { get; set; }
}