﻿using System.Text.Json;
using McMaster.Extensions.CommandLineUtils;
using VpnHood.Common;
using VpnHood.Common.TokenLegacy;
using VpnHood.Server.Access.Managers.File;

namespace VpnHood.Server.App;

public class FileAccessManagerCommand(FileAccessManager fileAccessManager)
{
    public void AddCommands(CommandLineApplication cmdApp)
    {
        cmdApp.Command("print", PrintToken);
        cmdApp.Command("gen", GenerateToken);
    }

    private void PrintToken(CommandLineApplication cmdApp)
    {
        cmdApp.Description = "Print a token";

        var tokenIdArg = cmdApp.Argument("tokenId", "tokenId to print");
        cmdApp.OnExecuteAsync(async _ =>
        {
            await PrintToken(tokenIdArg.Value!);
            return 0;
        });
    }

    private async Task PrintToken(string tokenId)
    {
        var accessItem = await fileAccessManager.AccessItem_Read(tokenId);
        if (accessItem == null) throw new KeyNotFoundException($"Token does not exist! tokenId: {tokenId}");
        var hostName = accessItem.Token.ServerToken.HostName + (accessItem.Token.ServerToken.IsValidHostName ? "" : " (Fake)");
        var endPoints = accessItem.Token.ServerToken.HostEndPoints?.Select(x => x.ToString()) ?? Array.Empty<string>();

        Console.WriteLine();
        Console.WriteLine("Access Details:");
        Console.WriteLine(JsonSerializer.Serialize(accessItem.AccessUsage, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine();
        Console.WriteLine($"{nameof(Token.SupportId)}: {accessItem.Token.SupportId}");
        Console.WriteLine($"{nameof(ServerToken.HostEndPoints)}: {string.Join(",", endPoints)}");
        Console.WriteLine($"{nameof(ServerToken.HostName)}: {hostName}");
        Console.WriteLine($"{nameof(ServerToken.HostPort)}: {accessItem.Token.ServerToken.HostPort}");
        Console.WriteLine($"TokenUpdateUrl: {accessItem.Token.ServerToken.Url}");
        Console.WriteLine("---");

#pragma warning disable CS0618 // Type or member is obsolete
        Console.WriteLine();
        Console.WriteLine("AccessKey (OldVersion 3.3.450 or lower):");
        Console.WriteLine();
        Console.WriteLine(TokenV3.FromToken(accessItem.Token).ToAccessKey());
        Console.WriteLine("---");
#pragma warning restore CS0618 // Type or member is obsolete

        Console.WriteLine();
        Console.WriteLine("AccessKey (NewVersion 3.3.451 or upper):");
        Console.WriteLine();
        Console.WriteLine(accessItem.Token.ToAccessKey());
        Console.WriteLine("---");
        Console.WriteLine();
    }

    private void GenerateToken(CommandLineApplication cmdApp)
    {
        var accessManager = fileAccessManager;

        cmdApp.Description = "Generate a token";
        var nameOption = cmdApp.Option("-name", "TokenName. Default: <NoName>", CommandOptionType.SingleValue);
        var maxClientOption = cmdApp.Option("-maxClient", "MaximumClient. Default: 2", CommandOptionType.SingleValue);
        var maxTrafficOptions = cmdApp.Option("-maxTraffic", "MaximumTraffic in MB. Default: unlimited", CommandOptionType.SingleValue);
        var expirationTimeOption = cmdApp.Option("-expire", "ExpirationTime. Default: Never Expire. Format: 2030/01/25", CommandOptionType.SingleValue);

        cmdApp.OnExecuteAsync(async _ =>
        {
            var accessItem = accessManager.AccessItem_Create(
                tokenName: nameOption.HasValue() ? nameOption.Value() : null,
                maxClientCount: maxClientOption.HasValue() ? int.Parse(maxClientOption.Value()!) : 2,
                maxTrafficByteCount: maxTrafficOptions.HasValue() ? int.Parse(maxTrafficOptions.Value()!) * 1_000_000 : 0,
                expirationTime: expirationTimeOption.HasValue() ? DateTime.Parse(expirationTimeOption.Value()!) : null
                );

            Console.WriteLine("The following token has been generated: ");
            await PrintToken(accessItem.Token.TokenId);
            Console.WriteLine($"Store Token Count: {accessManager.AccessItem_Count()}");
            return 0;
        });
    }
}