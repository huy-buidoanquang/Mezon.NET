namespace Mezon.Net.Client;

public readonly record struct MessageCallLog(
    bool IsVideo,
    int CallLogType,
    bool? ShowCallBack = null);
