using System;
using Mezon.Net.Client;

namespace Mezon.Net.Sdk.Builders;

/// <summary>Builds call log metadata for <see cref="MessageContentBuilder"/>.</summary>
public sealed class MessageCallLogBuilder
{
    private bool _built;
    private bool _isVideo;
    private int _callLogType;
    private bool? _showCallBack;

    public MessageCallLogBuilder SetVideo(bool isVideo)
    {
        EnsureMutable();
        _isVideo = isVideo;
        return this;
    }

    public MessageCallLogBuilder SetCallLogType(int callLogType)
    {
        EnsureMutable();
        _callLogType = callLogType;
        return this;
    }

    public MessageCallLogBuilder SetShowCallBack(bool? showCallBack)
    {
        EnsureMutable();
        _showCallBack = showCallBack;
        return this;
    }

    public MessageCallLog Build()
    {
        EnsureMutable();
        _built = true;
        return new MessageCallLog(_isVideo, _callLogType, _showCallBack);
    }

    private void EnsureMutable()
    {
        if (_built)
        {
            throw new InvalidOperationException("Builder already built.");
        }
    }
}
