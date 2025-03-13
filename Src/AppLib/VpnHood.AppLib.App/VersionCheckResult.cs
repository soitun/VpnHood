﻿namespace VpnHood.AppLib;

internal class VersionCheckResult
{
    public required DateTime CheckedTime { get; init; }
    public required Version LocalVersion { get; init; }
    public required VersionStatus VersionStatus { get; init; }
    public required PublishInfo PublishInfo { get; init; }

    public PublishInfo? GetNewerPublishInfo()
    {
        return VersionStatus is VersionStatus.Deprecated or VersionStatus.Old
            ? PublishInfo
            : null;
    }
}