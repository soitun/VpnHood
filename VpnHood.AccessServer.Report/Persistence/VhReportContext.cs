﻿using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using VpnHood.AccessServer.Models;

namespace VpnHood.AccessServer.Report.Persistence;
//using static Microsoft.EntityFrameworkCore.NpgsqlIndexBuilderExtensions;

// ReSharper disable once PartialTypeWithSinglePart
public partial class VhReportContext : DbContext
{
    public virtual DbSet<ServerStatusModel> ServerStatuses { get; set; } = default!;
    public virtual DbSet<AccessUsageModel> AccessUsages { get; set; } = default!;
    public virtual DbSet<SessionModel> Sessions { get; set; } = default!;

    public VhReportContext(DbContextOptions<VhReportContext> options)
        : base(options)
    {
    }

    public async Task<IDbContextTransaction?> WithNoLockTransaction()
    {
        Database.SetCommandTimeout(600);
        return Database.CurrentTransaction == null
            ? await Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted)
            : null;
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HavePrecision(0);

        configurationBuilder.Properties<string>()
            .HaveMaxLength(4000);
    }

    public static int DateDiffMinute(DateTime start, DateTime end)
    {
        throw new Exception("Should not be called!");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //modelBuilder.HasAnnotation("Relational:Collation", "Latin1_General_100_CI_AS_SC_UTF8");

        modelBuilder.Entity<ServerStatusModel>(entity =>
        {
            entity.HasKey(e => e.ServerStatusId);

            modelBuilder.HasDbFunction(() => DateDiffMinute(default, default))
                .IsBuiltIn()
                .HasTranslation(parameters =>
                    new SqlFunctionExpression("EXTRACT", parameters.Prepend(new SqlFragmentExpression("MINUTE FROM")),
                        true, new[] { false, true, true }, typeof(int), null));

            entity
                .ToTable(nameof(ServerStatuses))
                .HasKey(e => e.ServerStatusId);

            entity.Property(e => e.ServerStatusId)
                .ValueGeneratedNever();

            entity
                .HasIndex(e => new { e.ProjectId, e.CreatedTime })
                .IncludeProperties(e => new
                {
                    e.ServerId,
                    e.SessionCount,
                    e.TunnelSendSpeed,
                    e.TunnelReceiveSpeed
                });

            entity
                .HasIndex(e => new { e.ServerId, e.CreatedTime })
                .IncludeProperties(e => new
                {
                    e.SessionCount,
                    e.TunnelSendSpeed,
                    e.TunnelReceiveSpeed
                });

            entity.Ignore(x => x.Project);
            entity.Ignore(x => x.Server);
            entity.Ignore(x => x.IsLast);
        });

        modelBuilder.Entity<AccessUsageModel>(entity =>
        {
            entity.HasKey(e => e.AccessUsageId);

            entity
                .ToTable(nameof(AccessUsages))
                .HasKey(x => x.AccessUsageId);

            entity.Property(e => e.AccessUsageId)
                .ValueGeneratedNever();

            entity.HasIndex(e => new { e.ProjectId, e.CreatedTime })
                .IncludeProperties(e => new { e.SessionId, e.DeviceId, e.SentTraffic, e.ReceivedTraffic, e.AccessTokenId, e.ServerFarmId });

            entity.HasIndex(e => new { e.ProjectId, e.AccessId, e.CreatedTime })
                .IncludeProperties(e => new { e.SessionId, e.DeviceId, e.SentTraffic, e.ReceivedTraffic });

            entity.HasIndex(e => new { e.ProjectId, e.ServerFarmId, e.CreatedTime })
                .IncludeProperties(e => new { e.SessionId, e.DeviceId, e.SentTraffic, e.ReceivedTraffic });

            entity.HasIndex(e => new { e.ProjectId, e.AccessTokenId, e.CreatedTime })
                .IncludeProperties(e => new { e.SessionId, e.DeviceId, e.SentTraffic, e.ReceivedTraffic });

            entity.HasIndex(e => new { e.ProjectId, e.ServerId, e.CreatedTime })
                .IncludeProperties(e => new { e.SessionId, e.DeviceId, e.SentTraffic, e.ReceivedTraffic });

            entity.HasIndex(e => new { e.ProjectId, e.DeviceId, e.CreatedTime })
                .IncludeProperties(e => new { e.SessionId, e.SentTraffic, e.ReceivedTraffic });
        });


        modelBuilder.Entity<SessionModel>(entity =>
        {
            entity.HasKey(e => e.SessionId);

            entity.HasIndex(e => new { e.ServerId, e.CreatedTime });

            entity.Property(e => e.SessionId)
                .ValueGeneratedNever();

            entity.Property(e => e.Country)
                .HasMaxLength(10);

            entity.Property(e => e.DeviceIp)
                .HasMaxLength(50);

            entity.Property(e => e.ClientVersion)
                .HasMaxLength(20);

            entity.Ignore(e => e.Server);
            entity.Ignore(e => e.Device);
            entity.Ignore(e => e.Access);
            entity.Ignore(e => e.IsArchived);
            entity.Ignore(e => e.SessionKey);
        });

        // ReSharper disable once InvocationIsSkipped
        OnModelCreatingPartial(modelBuilder);
    }

    // ReSharper disable once PartialMethodWithSinglePart
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}