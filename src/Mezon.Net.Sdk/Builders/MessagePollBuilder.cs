using System;
using System.Collections.Generic;
using Mezon.Net.Client;

namespace Mezon.Net.Sdk.Builders;

/// <summary>Builds a native poll payload for <see cref="MessageContentBuilder"/>.</summary>
public sealed class MessagePollBuilder
{
    private readonly List<MessagePollAnswer> _answers = new();
    private readonly List<string> _answerEmojiIds = new();
    private readonly List<int> _answerCounts = new();
    private readonly List<int> _userVotes = new();
    private bool _built;
    private string? _question;
    private string? _questionEmojiId;
    private long? _expireTime;
    private bool? _isClosed;
    private int? _totalVotes;
    private bool? _allowMultipleAnswers;
    private long? _id;
    private long? _expireAt;
    private int? _type;

    public MessagePollBuilder SetQuestion(string? question)
    {
        EnsureMutable();
        _question = question;
        return this;
    }

    public MessagePollBuilder SetQuestionEmojiId(string? emojiId)
    {
        EnsureMutable();
        _questionEmojiId = emojiId;
        return this;
    }

    public MessagePollBuilder AddAnswer(int index, string label)
    {
        EnsureMutable();
        _answers.Add(new MessagePollAnswer(index, label));
        return this;
    }

    public MessagePollBuilder SetAnswers(IReadOnlyList<MessagePollAnswer>? answers)
    {
        EnsureMutable();
        _answers.Clear();
        if (answers is not null)
        {
            _answers.AddRange(answers);
        }

        return this;
    }

    public MessagePollBuilder SetAnswerEmojiIds(IReadOnlyList<string>? values)
    {
        EnsureMutable();
        Replace(_answerEmojiIds, values);
        return this;
    }

    public MessagePollBuilder SetAnswerCounts(IReadOnlyList<int>? values)
    {
        EnsureMutable();
        Replace(_answerCounts, values);
        return this;
    }

    public MessagePollBuilder SetExpireTime(long? expireTime)
    {
        EnsureMutable();
        _expireTime = expireTime;
        return this;
    }

    public MessagePollBuilder SetClosed(bool? isClosed)
    {
        EnsureMutable();
        _isClosed = isClosed;
        return this;
    }

    public MessagePollBuilder SetTotalVotes(int? totalVotes)
    {
        EnsureMutable();
        _totalVotes = totalVotes;
        return this;
    }

    public MessagePollBuilder SetAllowMultipleAnswers(bool? allowMultipleAnswers)
    {
        EnsureMutable();
        _allowMultipleAnswers = allowMultipleAnswers;
        return this;
    }

    public MessagePollBuilder SetUserVotes(IReadOnlyList<int>? values)
    {
        EnsureMutable();
        Replace(_userVotes, values);
        return this;
    }

    public MessagePollBuilder SetId(long? id)
    {
        EnsureMutable();
        _id = id;
        return this;
    }

    public MessagePollBuilder SetExpireAt(long? expireAt)
    {
        EnsureMutable();
        _expireAt = expireAt;
        return this;
    }

    public MessagePollBuilder SetType(int? type)
    {
        EnsureMutable();
        _type = type;
        return this;
    }

    public MessagePoll Build()
    {
        EnsureMutable();
        _built = true;
        return new MessagePoll(
            _question,
            _questionEmojiId,
            _answers.Count == 0 ? null : _answers.ToArray(),
            _answerEmojiIds.Count == 0 ? null : _answerEmojiIds.ToArray(),
            _answerCounts.Count == 0 ? null : _answerCounts.ToArray(),
            _expireTime,
            _isClosed,
            _totalVotes,
            _allowMultipleAnswers,
            _userVotes.Count == 0 ? null : _userVotes.ToArray(),
            _id,
            _expireAt,
            _type);
    }

    private void EnsureMutable()
    {
        if (_built)
        {
            throw new InvalidOperationException("Builder already built.");
        }
    }

    private static void Replace<T>(List<T> target, IReadOnlyList<T>? source)
    {
        target.Clear();
        if (source is not null)
        {
            target.AddRange(source);
        }
    }
}
