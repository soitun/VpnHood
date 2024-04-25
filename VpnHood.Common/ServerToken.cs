﻿using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using VpnHood.Common.Converters;
using VpnHood.Common.Utils;

namespace VpnHood.Common;

public class ServerToken 
{
    [JsonPropertyName("ct")]
    public required DateTime CreatedTime { get; set; }

    [JsonPropertyName("hname")]
    public required string HostName { get; set; }

    [JsonPropertyName("hport")]
    public required int HostPort { get; set; }

    [JsonPropertyName("isv")]
    public required bool IsValidHostName { get; set; }

    [JsonPropertyName("sec")]
    public required byte[]? Secret { get; set; }

    [JsonPropertyName("ch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? CertificateHash { get; set; }

    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Url { get; set; }

    [JsonPropertyName("ep")]
    [JsonConverter(typeof(ArrayConverter<IPEndPoint, IPEndPointConverter>))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IPEndPoint[]? HostEndPoints { get; set; }

    [JsonPropertyName("regions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public HostRegion[]? Regions { get; set; }

    public string Encrypt()
    {
        var json = JsonSerializer.Serialize(this);

        // generate IV
        using var rng = RandomNumberGenerator.Create();
        var iv = new byte[16];
        rng.GetBytes(iv);
        
        // aes
        using var aesAlg = Aes.Create();
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Key = Secret ?? throw new Exception("There is no Secret in ServerToken.");
        aesAlg.IV = iv;
        aesAlg.Padding = PaddingMode.PKCS7;

        using var msEncrypt = new MemoryStream();

        // dispose CryptoStream and StreamWriter before using msEncrypt.ToArray()
        using (var encryptor = aesAlg.CreateEncryptor())
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var streamWriter = new StreamWriter(csEncrypt))
        {
            streamWriter.Write(json);
            streamWriter.Flush();
        }

        var serverToken = Convert.ToBase64String(iv) + "." + Convert.ToBase64String(msEncrypt.ToArray());
        return serverToken;
    }

    public static ServerToken Decrypt(byte[] serverSecret, string base64)
    {
        var parts = base64.Trim().Split('.');
        if (parts.Length != 2)
            throw new FormatException("Could not parse server token data.");    

        // aes
        using var aesAlg = Aes.Create();
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Key = serverSecret;
        aesAlg.IV = Convert.FromBase64String(parts[0]);
        aesAlg.Padding = PaddingMode.PKCS7;

        using var decryptor = aesAlg.CreateDecryptor();
        using var msEncrypt = new MemoryStream(Convert.FromBase64String(parts[1]));
        using var csEncrypt = new CryptoStream(msEncrypt, decryptor, CryptoStreamMode.Read);
        using var streamReader = new StreamReader(csEncrypt);
        
        var json = streamReader.ReadToEnd();
        var serverToken = VhUtil.JsonDeserialize<ServerToken>(json);
        return serverToken;
    }

    public bool IsTokenUpdated(ServerToken newServerToken)
    {
        // create first server token by removing its created time
        var serverToken1 = VhUtil.JsonClone<ServerToken>(this);
        serverToken1.CreatedTime = DateTime.MinValue;

        // create second server token by removing its created time
        var serverToken2 = VhUtil.JsonClone<ServerToken>(newServerToken);
        serverToken2.CreatedTime = DateTime.MinValue;

        // compare
        if (JsonSerializer.Serialize(serverToken1) == JsonSerializer.Serialize(serverToken2))
            return false;

        // if token are not equal, check if new token CreatedTime is newer or equal.
        // If created time is equal assume new token is updated because there is change in token.
        return newServerToken.CreatedTime >= CreatedTime;
    }
}
