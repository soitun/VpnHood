﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VpnHood.AccessServer.Dtos;
using VpnHood.AccessServer.Report.Services;
using VpnHood.AccessServer.Security;
using VpnHood.AccessServer.Services;
using ServerStatusHistory = VpnHood.AccessServer.Dtos.ServerStatusHistory;

namespace VpnHood.AccessServer.Controllers;

[ApiController]
[Authorize]
[Route("/api/v{version:apiVersion}/projects/{projectId}/servers")]
public class ServersController(
    UsageReportService usageReportService,
    ServerService serverService,
    SubscriptionService subscriptionService)
    : ControllerBase
{
    [HttpPost]
    [AuthorizeProjectPermission(Permissions.ServerWrite)]
    public Task<VpnServer> Create(Guid projectId, ServerCreateParams createParams)
    {
        return serverService.Create(projectId, createParams);
    }

    [HttpPatch("{serverId:guid}")]
    [AuthorizeProjectPermission(Permissions.ServerWrite)]
    public Task<VpnServer> Update(Guid projectId, Guid serverId, ServerUpdateParams updateParams)
    {
        return serverService.Update(projectId, serverId, updateParams);
    }

    [HttpGet("{serverId:guid}")]
    [AuthorizeProjectPermission(Permissions.ProjectRead)]
    public async Task<ServerData> Get(Guid projectId, Guid serverId)
    {
        var list = await serverService.List(projectId, serverId: serverId);
        return list.Single();
    }

    [HttpDelete("{serverId:guid}")]
    [AuthorizeProjectPermission(Permissions.ServerWrite)]
    public Task Delete(Guid projectId, Guid serverId)
    {
        return serverService.Delete(projectId, serverId);
    }

    [HttpGet]
    [AuthorizeProjectPermission(Permissions.ProjectRead)]
    public Task<ServerData[]> List(Guid projectId, string? search = null, Guid? serverId = null, Guid? serverFarmId = null,
        int recordIndex = 0, int recordCount = 1000)
    {
        return serverService.List(projectId, search: search, serverId: serverId, serverFarmId: serverFarmId, recordIndex, recordCount);
    }

    [HttpPost("{serverId:guid}/reconfigure")]
    [AuthorizeProjectPermission(Permissions.ServerInstall)]
    public Task Reconfigure(Guid projectId, Guid serverId)
    {
        return serverService.Reconfigure(projectId, serverId);
    }
    
    [HttpPost("{serverId}/install-by-ssh-user-password")]
    [AuthorizeProjectPermission(Permissions.ServerInstall)]
    public Task InstallBySshUserPassword(Guid projectId, Guid serverId, ServerInstallBySshUserPasswordParams installParams)
    {
        return serverService.InstallBySshUserPassword(projectId, serverId, installParams);
    }

    [HttpPost("{serverId}/install-by-ssh-user-key")]
    [AuthorizeProjectPermission(Permissions.ServerInstall)]
    public Task InstallBySshUserKey(Guid projectId, Guid serverId, ServerInstallBySshUserKeyParams installParams)
    {
        return serverService.InstallBySshUserKey(projectId, serverId, installParams);
    }
    
    [HttpGet("{serverId}/install/manual")]
    [AuthorizeProjectPermission(Permissions.ServerInstall)]
    public Task<ServerInstallManual> GetInstallManual(Guid projectId, Guid serverId)
    {
        return serverService.GetInstallManual(projectId, serverId);
    }

    [HttpGet("status-summary")]
    [AuthorizeProjectPermission(Permissions.ProjectRead)]
    public Task<ServersStatusSummary> GetStatusSummary(Guid projectId, Guid? serverFarmId = null)
    {
        return serverService.GetStatusSummary(projectId, serverFarmId);
    }

    [HttpGet("status-history")]
    [AuthorizeProjectPermission(Permissions.ProjectRead)]
    public async Task<ServerStatusHistory[]> GetStatusHistory(Guid projectId,
        DateTime? usageBeginTime, DateTime? usageEndTime = null, Guid? serverId = null)
    {
        if (usageBeginTime == null) throw new ArgumentNullException(nameof(usageBeginTime));
        await subscriptionService.VerifyUsageQueryPermission(projectId, usageBeginTime, usageEndTime);

        var ret = await usageReportService.GetServersStatusHistory(projectId, usageBeginTime.Value, usageEndTime, serverId);
        return ret;
    }
}