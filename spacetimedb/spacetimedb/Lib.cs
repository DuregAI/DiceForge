using System;
using SpacetimeDB;

public static partial class Module
{
    private const int FeedbackMessageMaxLength = 1000;
    private const int PlayerNameMaxLength = 23;

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
        public string player_guid;
        public string player_name;
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
        public string player_guid;
        public string player_name;
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
        public string player_guid;
        public string player_name;
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
        public string player_guid;
        public string player_name;
        public string track_id;
        public long track_elapsed_ms;
        public long created_at_unix_ms_utc;
        public string build_version;
        public string scene_name;
    }

    [SpacetimeDB.Table(Accessor = "player_name_change_event", Public = true)]
    public partial struct PlayerNameChangeEvent
    {
        [SpacetimeDB.PrimaryKey]
        public string event_id;
        public string session_id;
        public string player_guid;
        public string previous_player_name;
        public string new_player_name;
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
        string player_guid,
        string player_name,
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
            session_id = NormalizeOptional(session_id),
            player_guid = NormalizeOptional(player_guid),
            player_name = NormalizePlayerName(player_name),
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
        string player_guid,
        string player_name,
        string category,
        string message,
        long created_at_unix_ms_utc,
        string build_version,
        string scene_name)
    {
        string trimmedCategory = NormalizeRequired(category, nameof(category));
        string trimmedMessage = NormalizeRequired(message, nameof(message));

        if (string.IsNullOrWhiteSpace(feedback_id))
            throw new ArgumentException("feedback_id must not be empty.", nameof(feedback_id));

        if (trimmedMessage.Length > FeedbackMessageMaxLength)
            throw new ArgumentOutOfRangeException(nameof(message), $"message must not exceed {FeedbackMessageMaxLength} characters.");

        if (created_at_unix_ms_utc <= 0)
            throw new ArgumentOutOfRangeException(nameof(created_at_unix_ms_utc), "created_at_unix_ms_utc must be positive.");

        ctx.Db.feedback_entry.Insert(new FeedbackEntry
        {
            feedback_id = feedback_id,
            session_id = NormalizeOptional(session_id),
            player_guid = NormalizeOptional(player_guid),
            player_name = NormalizePlayerName(player_name),
            category = trimmedCategory,
            message = trimmedMessage,
            created_at_unix_ms_utc = created_at_unix_ms_utc,
            build_version = NormalizeOptional(build_version),
            scene_name = NormalizeOptional(scene_name),
        });
    }

    [SpacetimeDB.Reducer]
    public static void submit_music_dislike(
        ReducerContext ctx,
        string event_id,
        string session_id,
        string player_guid,
        string player_name,
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
            session_id = NormalizeOptional(session_id),
            player_guid = NormalizeOptional(player_guid),
            player_name = NormalizePlayerName(player_name),
            track_id = track_id.Trim(),
            track_elapsed_ms = track_elapsed_ms,
            created_at_unix_ms_utc = created_at_unix_ms_utc,
            build_version = NormalizeOptional(build_version),
            scene_name = NormalizeOptional(scene_name),
        });
    }

    [SpacetimeDB.Reducer]
    public static void submit_music_skip(
        ReducerContext ctx,
        string event_id,
        string session_id,
        string player_guid,
        string player_name,
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
            session_id = NormalizeOptional(session_id),
            player_guid = NormalizeOptional(player_guid),
            player_name = NormalizePlayerName(player_name),
            track_id = track_id.Trim(),
            track_elapsed_ms = track_elapsed_ms,
            created_at_unix_ms_utc = created_at_unix_ms_utc,
            build_version = NormalizeOptional(build_version),
            scene_name = NormalizeOptional(scene_name),
        });
    }

    [SpacetimeDB.Reducer]
    public static void submit_player_name_change(
        ReducerContext ctx,
        string event_id,
        string session_id,
        string player_guid,
        string previous_player_name,
        string new_player_name,
        long created_at_unix_ms_utc,
        string build_version,
        string scene_name)
    {
        if (string.IsNullOrWhiteSpace(event_id))
            throw new ArgumentException("event_id must not be empty.", nameof(event_id));

        string normalizedPreviousPlayerName = NormalizePlayerName(previous_player_name);
        string normalizedNewPlayerName = NormalizePlayerName(new_player_name);

        if (string.IsNullOrWhiteSpace(normalizedNewPlayerName))
            throw new ArgumentException("new_player_name must not be empty.", nameof(new_player_name));

        if (created_at_unix_ms_utc <= 0)
            throw new ArgumentOutOfRangeException(nameof(created_at_unix_ms_utc), "created_at_unix_ms_utc must be positive.");

        ctx.Db.player_name_change_event.Insert(new PlayerNameChangeEvent
        {
            event_id = event_id,
            session_id = NormalizeOptional(session_id),
            player_guid = NormalizeOptional(player_guid),
            previous_player_name = normalizedPreviousPlayerName,
            new_player_name = normalizedNewPlayerName,
            created_at_unix_ms_utc = created_at_unix_ms_utc,
            build_version = NormalizeOptional(build_version),
            scene_name = NormalizeOptional(scene_name),
        });
    }

    private static string NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        string normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{paramName} must not be empty.", paramName);

        return normalized;
    }

    private static string NormalizePlayerName(string value)
    {
        string normalized = NormalizeOptional(value);
        if (normalized.Length > PlayerNameMaxLength)
            throw new ArgumentOutOfRangeException(nameof(value), $"player name must not exceed {PlayerNameMaxLength} characters.");

        return normalized;
    }
}
