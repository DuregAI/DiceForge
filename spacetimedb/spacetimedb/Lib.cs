using System;
using SpacetimeDB;

public static partial class Module
{
    private const int FeedbackMessageMaxLength = 1000;

    [SpacetimeDB.Table(Accessor = "performance_session_summary", Public = true)]
    public partial struct PerformanceSessionSummary
    {
        [SpacetimeDB.PrimaryKey]
        public string session_id;
        public long started_at_unix_ms_utc;
        public long ended_at_unix_ms_utc;
        public double duration_seconds;
        public string build_version;
        public string platform;
        public string operating_system;
        public string cpu_name;
        public string gpu_name;
        public int screen_width;
        public int screen_height;
        public double average_fps;
        public double minimum_window_fps;
        public double worst_spike_ms;
        public int battles_started_count;
        public int battles_ended_count;
    }

    [SpacetimeDB.Table(Accessor = "like_event", Public = true)]
    public partial struct LikeEvent
    {
        [SpacetimeDB.PrimaryKey]
        public string like_id;
        public string session_id;
        public string target_type;
        public string target_id;
        public long created_at_unix_ms_utc;
    }

    [SpacetimeDB.Table(Accessor = "feedback_entry", Public = true)]
    public partial struct FeedbackEntry
    {
        [SpacetimeDB.PrimaryKey]
        public string feedback_id;
        public string session_id;
        public string category;
        public string message;
        public long created_at_unix_ms_utc;
        public string build_version;
        public string scene_name;
    }

    [SpacetimeDB.Table(Accessor = "music_dislike_event", Public = true)]
    public partial struct MusicDislikeEvent
    {
        [SpacetimeDB.PrimaryKey]
        public string event_id;
        public string session_id;
        public string track_id;
        public long track_elapsed_ms;
        public long created_at_unix_ms_utc;
        public string build_version;
        public string scene_name;
    }

    [SpacetimeDB.Table(Accessor = "music_skip_event", Public = true)]
    public partial struct MusicSkipEvent
    {
        [SpacetimeDB.PrimaryKey]
        public string event_id;
        public string session_id;
        public string track_id;
        public long track_elapsed_ms;
        public long created_at_unix_ms_utc;
        public string build_version;
        public string scene_name;
    }

    [SpacetimeDB.Reducer]
    public static void submit_performance_session_summary(
        ReducerContext ctx,
        string session_id,
        long started_at_unix_ms_utc,
        long ended_at_unix_ms_utc,
        double duration_seconds,
        string build_version,
        string platform,
        string operating_system,
        string cpu_name,
        string gpu_name,
        int screen_width,
        int screen_height,
        double average_fps,
        double minimum_window_fps,
        double worst_spike_ms,
        int battles_started_count,
        int battles_ended_count)
    {
        if (string.IsNullOrWhiteSpace(session_id))
            throw new ArgumentException("session_id must not be empty.", nameof(session_id));

        if (ended_at_unix_ms_utc < started_at_unix_ms_utc)
            throw new ArgumentException("ended_at_unix_ms_utc must not be earlier than started_at_unix_ms_utc.");

        if (duration_seconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(duration_seconds), "duration_seconds must not be negative.");

        if (screen_width < 0)
            throw new ArgumentOutOfRangeException(nameof(screen_width), "screen_width must not be negative.");

        if (screen_height < 0)
            throw new ArgumentOutOfRangeException(nameof(screen_height), "screen_height must not be negative.");

        if (average_fps < 0d)
            throw new ArgumentOutOfRangeException(nameof(average_fps), "average_fps must not be negative.");

        if (minimum_window_fps < 0d)
            throw new ArgumentOutOfRangeException(nameof(minimum_window_fps), "minimum_window_fps must not be negative.");

        if (worst_spike_ms < 0d)
            throw new ArgumentOutOfRangeException(nameof(worst_spike_ms), "worst_spike_ms must not be negative.");

        if (battles_started_count < 0)
            throw new ArgumentOutOfRangeException(nameof(battles_started_count), "battles_started_count must not be negative.");

        if (battles_ended_count < 0)
            throw new ArgumentOutOfRangeException(nameof(battles_ended_count), "battles_ended_count must not be negative.");

        ctx.Db.performance_session_summary.Insert(new PerformanceSessionSummary
        {
            session_id = session_id,
            started_at_unix_ms_utc = started_at_unix_ms_utc,
            ended_at_unix_ms_utc = ended_at_unix_ms_utc,
            duration_seconds = duration_seconds,
            build_version = build_version,
            platform = platform,
            operating_system = operating_system,
            cpu_name = cpu_name,
            gpu_name = gpu_name,
            screen_width = screen_width,
            screen_height = screen_height,
            average_fps = average_fps,
            minimum_window_fps = minimum_window_fps,
            worst_spike_ms = worst_spike_ms,
            battles_started_count = battles_started_count,
            battles_ended_count = battles_ended_count,
        });
    }

    [SpacetimeDB.Reducer]
    public static void submit_like(
        ReducerContext ctx,
        string like_id,
        string session_id,
        string target_type,
        string target_id,
        long created_at_unix_ms_utc)
    {
        if (string.IsNullOrWhiteSpace(like_id))
            throw new ArgumentException("like_id must not be empty.", nameof(like_id));

        if (string.IsNullOrWhiteSpace(target_type))
            throw new ArgumentException("target_type must not be empty.", nameof(target_type));

        if (string.IsNullOrWhiteSpace(target_id))
            throw new ArgumentException("target_id must not be empty.", nameof(target_id));

        if (created_at_unix_ms_utc <= 0)
            throw new ArgumentOutOfRangeException(nameof(created_at_unix_ms_utc), "created_at_unix_ms_utc must be positive.");

        ctx.Db.like_event.Insert(new LikeEvent
        {
            like_id = like_id,
            session_id = string.IsNullOrWhiteSpace(session_id) ? string.Empty : session_id,
            target_type = target_type,
            target_id = target_id,
            created_at_unix_ms_utc = created_at_unix_ms_utc,
        });
    }

    [SpacetimeDB.Reducer]
    public static void submit_feedback(
        ReducerContext ctx,
        string feedback_id,
        string session_id,
        string category,
        string message,
        long created_at_unix_ms_utc,
        string build_version,
        string scene_name)
    {
        string trimmedCategory = category == null ? string.Empty : category.Trim();
        string trimmedMessage = message == null ? string.Empty : message.Trim();

        if (string.IsNullOrWhiteSpace(feedback_id))
            throw new ArgumentException("feedback_id must not be empty.", nameof(feedback_id));

        if (string.IsNullOrWhiteSpace(trimmedCategory))
            throw new ArgumentException("category must not be empty.", nameof(category));

        if (string.IsNullOrWhiteSpace(trimmedMessage))
            throw new ArgumentException("message must not be empty.", nameof(message));

        if (trimmedMessage.Length > FeedbackMessageMaxLength)
            throw new ArgumentOutOfRangeException(nameof(message), $"message must not exceed {FeedbackMessageMaxLength} characters.");

        if (created_at_unix_ms_utc <= 0)
            throw new ArgumentOutOfRangeException(nameof(created_at_unix_ms_utc), "created_at_unix_ms_utc must be positive.");

        ctx.Db.feedback_entry.Insert(new FeedbackEntry
        {
            feedback_id = feedback_id,
            session_id = string.IsNullOrWhiteSpace(session_id) ? string.Empty : session_id,
            category = trimmedCategory,
            message = trimmedMessage,
            created_at_unix_ms_utc = created_at_unix_ms_utc,
            build_version = string.IsNullOrWhiteSpace(build_version) ? string.Empty : build_version.Trim(),
            scene_name = string.IsNullOrWhiteSpace(scene_name) ? string.Empty : scene_name.Trim(),
        });
    }

    [SpacetimeDB.Reducer]
    public static void submit_music_dislike(
        ReducerContext ctx,
        string event_id,
        string session_id,
        string track_id,
        long track_elapsed_ms,
        long created_at_unix_ms_utc,
        string build_version,
        string scene_name)
    {
        if (string.IsNullOrWhiteSpace(event_id))
            throw new ArgumentException("event_id must not be empty.", nameof(event_id));

        if (string.IsNullOrWhiteSpace(track_id))
            throw new ArgumentException("track_id must not be empty.", nameof(track_id));

        if (track_elapsed_ms < 0)
            throw new ArgumentOutOfRangeException(nameof(track_elapsed_ms), "track_elapsed_ms must not be negative.");

        if (created_at_unix_ms_utc <= 0)
            throw new ArgumentOutOfRangeException(nameof(created_at_unix_ms_utc), "created_at_unix_ms_utc must be positive.");

        ctx.Db.music_dislike_event.Insert(new MusicDislikeEvent
        {
            event_id = event_id,
            session_id = string.IsNullOrWhiteSpace(session_id) ? string.Empty : session_id.Trim(),
            track_id = track_id.Trim(),
            track_elapsed_ms = track_elapsed_ms,
            created_at_unix_ms_utc = created_at_unix_ms_utc,
            build_version = string.IsNullOrWhiteSpace(build_version) ? string.Empty : build_version.Trim(),
            scene_name = string.IsNullOrWhiteSpace(scene_name) ? string.Empty : scene_name.Trim(),
        });
    }

    [SpacetimeDB.Reducer]
    public static void submit_music_skip(
        ReducerContext ctx,
        string event_id,
        string session_id,
        string track_id,
        long track_elapsed_ms,
        long created_at_unix_ms_utc,
        string build_version,
        string scene_name)
    {
        if (string.IsNullOrWhiteSpace(event_id))
            throw new ArgumentException("event_id must not be empty.", nameof(event_id));

        if (string.IsNullOrWhiteSpace(track_id))
            throw new ArgumentException("track_id must not be empty.", nameof(track_id));

        if (track_elapsed_ms < 0)
            throw new ArgumentOutOfRangeException(nameof(track_elapsed_ms), "track_elapsed_ms must not be negative.");

        if (created_at_unix_ms_utc <= 0)
            throw new ArgumentOutOfRangeException(nameof(created_at_unix_ms_utc), "created_at_unix_ms_utc must be positive.");

        ctx.Db.music_skip_event.Insert(new MusicSkipEvent
        {
            event_id = event_id,
            session_id = string.IsNullOrWhiteSpace(session_id) ? string.Empty : session_id.Trim(),
            track_id = track_id.Trim(),
            track_elapsed_ms = track_elapsed_ms,
            created_at_unix_ms_utc = created_at_unix_ms_utc,
            build_version = string.IsNullOrWhiteSpace(build_version) ? string.Empty : build_version.Trim(),
            scene_name = string.IsNullOrWhiteSpace(scene_name) ? string.Empty : scene_name.Trim(),
        });
    }
}
