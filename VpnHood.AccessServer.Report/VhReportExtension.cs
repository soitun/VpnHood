﻿using GrayMint.Common.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VpnHood.AccessServer.Report.Persistence;
using VpnHood.AccessServer.Report.Services;

namespace VpnHood.AccessServer.Report;

public static class VhReportExtension
{
    public static IServiceCollection AddVhReportServices(this IServiceCollection services, ReportServiceOptions reportServiceOptions)
    {
        services.AddDbContext<VhReportContext>(options => options.UseSqlServer(reportServiceOptions.ConnectionString));
        services.AddScoped<UsageReportService>();
        services.AddScoped<ReportWriterService>();
        services.AddSingleton(Options.Create(reportServiceOptions));
        return services;
    }

    public static async Task UseVhReportServices(this IServiceProvider serviceProvider, string[] args)
    {
        await GrayMintApp.CheckDatabaseCommand<VhReportContext>(serviceProvider, args);
    }
}
