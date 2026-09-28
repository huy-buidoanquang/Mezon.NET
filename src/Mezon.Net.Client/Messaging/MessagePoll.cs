using System.Collections.Generic;

namespace Mezon.Net.Client;

public sealed class MessagePoll
{
    public MessagePoll(
        string? question = null,
        string? questionEmojiId = null,
        IReadOnlyList<MessagePollAnswer>? answers = null,
        IReadOnlyList<string>? answerEmojiIds = null,
        IReadOnlyList<int>? answerCounts = null,
        long? expireTime = null,
        bool? isClosed = null,
        int? totalVotes = null,
        bool? allowMultipleAnswers = null,
        IReadOnlyList<int>? userVotes = null,
        long? id = null,
        long? expireAt = null,
        int? type = null)
    {
        Question = question;
        QuestionEmojiId = questionEmojiId;
        Answers = Copy(answers);
        AnswerEmojiIds = Copy(answerEmojiIds);
        AnswerCounts = Copy(answerCounts);
        ExpireTime = expireTime;
        IsClosed = isClosed;
        TotalVotes = totalVotes;
        AllowMultipleAnswers = allowMultipleAnswers;
        UserVotes = Copy(userVotes);
        Id = id;
        ExpireAt = expireAt;
        Type = type;
    }

    public string? Question { get; }
    public string? QuestionEmojiId { get; }
    public IReadOnlyList<MessagePollAnswer>? Answers { get; }
    public IReadOnlyList<string>? AnswerEmojiIds { get; }
    public IReadOnlyList<int>? AnswerCounts { get; }
    public long? ExpireTime { get; }
    public bool? IsClosed { get; }
    public int? TotalVotes { get; }
    public bool? AllowMultipleAnswers { get; }
    public IReadOnlyList<int>? UserVotes { get; }
    public long? Id { get; }
    public long? ExpireAt { get; }
    public int? Type { get; }

    private static IReadOnlyList<T>? Copy<T>(IReadOnlyList<T>? values)
        => values is null || values.Count == 0 ? values : new List<T>(values).ToArray();
}
