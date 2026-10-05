using System;
using SpacetimeDB;

// RLS is opt-in in Runtime 2.0.3. The database owner retains unrestricted SQL access.
#pragma warning disable STDB_UNSTABLE

public static partial class Module
{
    // This database predates the Init reducer. Its existing owner was verified via
    // GET /v1/database/diceforgelocaldev; identity is public, not an access token.
    // Newly created databases use the publisher identity captured in Init instead.
    private const string LegacyLocalOwnerIdentity =
        "c2006d0dbf6eed147c98ec862e455d0f109f1d3c0337c69e53d8b1eb51b16bef";
    private const string LegacyLocalDatabaseIdentity =
        "c2009f903eadca8c4eb7a92ea4baefc96725340d35fabfb6bd2112867e452995";

    [SpacetimeDB.Table(Accessor = "module_administrator")]
    public partial struct ModuleAdministrator
    {
        [SpacetimeDB.PrimaryKey]
        public Identity identity;
    }

    [SpacetimeDB.Table(Accessor = "player_progress", Public = true)]
    public partial struct PlayerProgress
    {
        [SpacetimeDB.PrimaryKey]
        public Identity identity;
        public string player_guid;
        public string player_name;
        public string chapter_id;
        public int current_level;
        public int completed_levels;
        public int total_levels;
        public long reset_epoch;
        public long updated_at_unix_ms_utc;
    }

    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter PlayerProgressVisibility = new Filter.Sql(
        "SELECT * FROM player_progress WHERE player_progress.identity = :sender");

    [SpacetimeDB.Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {
        ctx.Db.module_administrator.Insert(new ModuleAdministrator { identity = ctx.Sender });
    }

    [SpacetimeDB.Reducer]
    public static void sync_player_progress(
        ReducerContext ctx,
        string player_guid,
        string player_name,
        string chapter_id,
        int current_level,
        int completed_levels,
        int total_levels,
        long reset_epoch)
    {
        string guid = NormalizeRequired(player_guid, nameof(player_guid));
        if (!Guid.TryParse(guid, out _))
            throw new ArgumentException("player_guid must be a GUID.");
        string name = NormalizePlayerName(player_name);
        string chapter = NormalizeRequired(chapter_id, nameof(chapter_id));
        if (chapter.Length > 128)
            throw new ArgumentException("chapter_id must not exceed 128 characters.");
        if (total_levels < 1 || total_levels > 1000 || completed_levels < 0 || completed_levels > total_levels
            || current_level < 1 || current_level > total_levels)
            throw new ArgumentException("Invalid map progress summary.");

        if (ctx.Db.player_progress.identity.Find(ctx.Sender) is PlayerProgress existing)
        {
            if (reset_epoch != existing.reset_epoch)
                throw new InvalidOperationException("MAP_RESET_REQUIRED: refresh the subscribed player progress before saving.");
            ctx.Db.player_progress.identity.Update(existing with
            {
                player_guid = guid,
                player_name = name,
                chapter_id = chapter,
                current_level = current_level,
                completed_levels = completed_levels,
                total_levels = total_levels,
                updated_at_unix_ms_utc = ctx.Timestamp.MicrosecondsSinceUnixEpoch / 1000,
            });
            return;
        }

        if (reset_epoch != 0)
            throw new InvalidOperationException("New player registrations must start at reset epoch zero.");
        ctx.Db.player_progress.Insert(new PlayerProgress
        {
            identity = ctx.Sender,
            player_guid = guid,
            player_name = name,
            chapter_id = chapter,
            current_level = current_level,
            completed_levels = completed_levels,
            total_levels = total_levels,
            reset_epoch = 0,
            updated_at_unix_ms_utc = ctx.Timestamp.MicrosecondsSinceUnixEpoch / 1000,
        });
    }

    [SpacetimeDB.Reducer]
    public static void reset_player_map(ReducerContext ctx, Identity player_identity, long expected_reset_epoch)
    {
        RequireAdministrator(ctx);
        if (ctx.Db.player_progress.identity.Find(player_identity) is not PlayerProgress player)
            throw new ArgumentException("Player not found.");
        if (player.reset_epoch != expected_reset_epoch)
            throw new InvalidOperationException("Player changed since the admin page was loaded. Refresh before resetting.");
        ctx.Db.player_progress.identity.Update(player with
        {
            current_level = 1,
            completed_levels = 0,
            reset_epoch = checked(player.reset_epoch + 1),
            updated_at_unix_ms_utc = ctx.Timestamp.MicrosecondsSinceUnixEpoch / 1000,
        });
    }

    private static void RequireAdministrator(ReducerContext ctx)
    {
        if (ctx.Db.module_administrator.identity.Find(ctx.Sender) is ModuleAdministrator)
            return;
        // Only the previously published local database may use this upgrade fallback.
        if (ctx.Identity == Identity.FromHexString(LegacyLocalDatabaseIdentity)
            && ctx.Sender == Identity.FromHexString(LegacyLocalOwnerIdentity))
            return;
        throw new InvalidOperationException("Only the database administrator can reset map progress.");
    }
}
