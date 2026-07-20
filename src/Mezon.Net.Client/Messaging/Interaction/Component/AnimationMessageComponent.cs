using System.Collections.Generic;

namespace Mezon.Net.Client
{
    public sealed class AnimationMessageComponent : MessageComponent
    {
        public AnimationMessageComponent(
            string id,
            string? urlImage = null,
            string? urlPosition = null,
            IReadOnlyList<string>? pool = null,
            IReadOnlyList<IReadOnlyList<string>>? poolRows = null,
            int? repeat = null,
            int? duration = null,
            bool? vertical = null,
            int? isResult = null)
            : base(id, MessageComponentType.Animation)
        {
            UrlImage = urlImage;
            UrlPosition = urlPosition;
            Pool = pool;
            PoolRows = poolRows;
            Repeat = repeat;
            Duration = duration;
            Vertical = vertical;
            IsResult = isResult;
        }

        public string? UrlImage { get; }
        public string? UrlPosition { get; }

        /// <summary>Flat pool (<c>AnimationConfig.pool</c> in mezon-sdk).</summary>
        public IReadOnlyList<string>? Pool { get; }

        /// <summary>2D pool (<c>IMessageAnimation.pool</c> in mezon utils).</summary>
        public IReadOnlyList<IReadOnlyList<string>>? PoolRows { get; }

        public int? Repeat { get; }
        public int? Duration { get; }
        public bool? Vertical { get; }
        public int? IsResult { get; }
    }
}
