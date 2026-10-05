using System;
using Diceforge.Map;
using Diceforge.Progression;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

namespace Diceforge.Integrations.SpacetimeDb
{
    // The beta still saves locally. This publishes a progress summary and obeys
    // administrator resets; it is deliberately not a cross-device save system.
    internal sealed class SpacetimeDbProgressSync : IDisposable
    {
        private readonly DbConnection _connection;
        private SubscriptionHandle _subscription;
        private bool _ready;
        private float _nextSyncAt;
        private long _serverResetEpoch;
        private string _identity;

        internal SpacetimeDbProgressSync(DbConnection connection)
        {
            _connection = connection;
            ProfileService.ProfileChanged += HandleProfileChanged;
            connection.Db.PlayerProgress.OnInsert += HandleInsert;
            connection.Db.PlayerProgress.OnUpdate += HandleUpdate;
            connection.Reducers.OnSyncPlayerProgress += HandleSyncResult;
        }

        internal void HandleConnected(Identity identity)
        {
            _identity = identity.ToString();
            // Row-level security limits this subscription to the authenticated player.
            _subscription = _connection.SubscriptionBuilder()
                .OnApplied(context =>
                {
                    foreach (var row in context.Db.PlayerProgress.Iter()) Observe(row);
                    _ready = true;
                    _nextSyncAt = 0f;
                })
                .OnError((context, exception) =>
                {
                    _ready = false;
                    Debug.LogWarning("[SpacetimeDb] Progress subscription failed: " + exception.Message);
                })
                .Subscribe(new[] { "SELECT * FROM player_progress" });
        }

        internal void HandleDisconnected() { _ready = false; }

        internal void Tick(float now)
        {
            if (!_ready || !_connection.IsActive || now < _nextSyncAt) return;
            if (!string.IsNullOrEmpty(ProfileService.LoadError)) return;
            if (ProfileService.Current.adminMapResetIdentity != _identity)
            {
                if (!ProfileService.TryBindAdminMapResetIdentity(_identity, out string identityError))
                {
                    _nextSyncAt = now + 5f;
                    Debug.LogWarning("[SpacetimeDb] Could not save player identity: " + identityError);
                    return;
                }
            }
            if (_serverResetEpoch > ProfileService.Current.adminMapResetEpoch)
            {
                // A persistent transition can still load its requested scene after
                // a direct reload. Let it finish before discarding the old run.
                if (Diceforge.Transitions.ScreenTransition.IsBusy) return;
                if (!ProfileService.TryApplyAdminMapReset(_serverResetEpoch, out string error))
                {
                    _nextSyncAt = now + 5f;
                    Debug.LogWarning("[SpacetimeDb] Could not save administrator reset: " + error);
                    return;
                }
                MapFlowRuntime.ClearRunContext();
                Time.timeScale = 1f;
                // Discard controllers holding a pre-reset run and clear tutorial
                // battle state through the existing navigation owner.
                TutorialFlow.ExitTutorial();
                Debug.Log("[SpacetimeDb] Map progress reset by administrator.");
            }
            var profile = ProfileService.Current;
            var map = MapDefinitionSO.LoadChapter("Chapter1");
            var chapter = profile.chapters.Find(item => item.chapterId == map.chapterId);
            int completed = 0;
            int current = 1;
            for (int i = 0; i < map.nodes.Count; i++)
            {
                if (chapter != null && chapter.state.IsCompleted(map.nodes[i].id)) completed++;
                if (chapter != null && chapter.state.currentNodeId == map.nodes[i].id) current = i + 1;
            }
            if (completed == map.nodes.Count) current = map.nodes.Count;
            _nextSyncAt = now + 30f;
            _connection.Reducers.SyncPlayerProgress(profile.playerGuid, ProfileService.GetDisplayName(),
                map.chapterId, current, completed, map.nodes.Count, profile.adminMapResetEpoch);
        }

        private void HandleProfileChanged()
        {
            _nextSyncAt = Mathf.Min(_nextSyncAt, Time.realtimeSinceStartup + 1f);
        }

        private void Observe(PlayerProgress row)
        {
            if (row.ResetEpoch > _serverResetEpoch) _nextSyncAt = 0f;
            _serverResetEpoch = Math.Max(_serverResetEpoch, row.ResetEpoch);
        }

        private void HandleInsert(EventContext context, PlayerProgress row) { Observe(row); }
        private void HandleUpdate(EventContext context, PlayerProgress oldRow, PlayerProgress newRow) { Observe(newRow); }
        private void HandleSyncResult(ReducerEventContext context, string playerGuid, string playerName,
            string chapterId, int currentLevel, int completedLevels, int totalLevels, long resetEpoch)
        {
            if (context.Event.Status is Status.Committed) return;
            Debug.LogWarning("[SpacetimeDb] Progress summary was not acknowledged; retrying.");
        }

        public void Dispose()
        {
            _ready = false;
            ProfileService.ProfileChanged -= HandleProfileChanged;
            _connection.Db.PlayerProgress.OnInsert -= HandleInsert;
            _connection.Db.PlayerProgress.OnUpdate -= HandleUpdate;
            _connection.Reducers.OnSyncPlayerProgress -= HandleSyncResult;
            if (_subscription != null && _connection.IsActive) _subscription.Unsubscribe();
        }
    }
}
