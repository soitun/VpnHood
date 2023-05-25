﻿namespace VpnHood.AccessServer.Models;

public class ServerFarmModel
{
    public Guid ServerFarmId { get; set; }
    public string ServerFarmName { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid CertificateId { get; set; }
    public Guid ServerProfileId { get; set; }
    public DateTime CreatedTime { get; set; }
    public bool UseHostName { get; set; }
    public byte[] Secret { get; set; } = default!;
    public bool IsDeleted { get; set; } = false;

    public virtual ServerProfileModel? ServerProfile { get; set; }
    public virtual ProjectModel? Project { get; set; }
    public virtual CertificateModel? Certificate { get; set; }
    public virtual ICollection<AccessTokenModel>? AccessTokens { get; set; }
    public virtual ICollection<ServerModel>? Servers { get; set; }
}