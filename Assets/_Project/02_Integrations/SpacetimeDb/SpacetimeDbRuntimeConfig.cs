using UnityEngine;

namespace Diceforge.Integrations.SpacetimeDb
{
    [CreateAssetMenu(menuName = "Diceforge/Integrations/SpacetimeDB Runtime Config", fileName = "SpacetimeDbRuntimeConfig")]
    public sealed class SpacetimeDbRuntimeConfig : ScriptableObject
    {
        private const string ResourcePath = "SpacetimeDbRuntimeConfig";
        private const string DefaultServerUri = "http://localhost:3000";
        private const string DefaultDatabaseName = "diceforgelocaldev";

        [SerializeField] private bool enabled = true;
        [SerializeField] private string serverUri = DefaultServerUri;
        [SerializeField] private string databaseName = DefaultDatabaseName;

        private static SpacetimeDbRuntimeConfig _current;

        public bool Enabled => enabled;
        public string ServerUri => string.IsNullOrWhiteSpace(serverUri) ? DefaultServerUri : serverUri.Trim();
        public string DatabaseName => string.IsNullOrWhiteSpace(databaseName) ? DefaultDatabaseName : databaseName.Trim();

        public static SpacetimeDbRuntimeConfig Current
        {
            get
            {
                if (_current != null)
                    return _current;

                _current = Resources.Load<SpacetimeDbRuntimeConfig>(ResourcePath);
                if (_current == null)
                {
                    _current = CreateInstance<SpacetimeDbRuntimeConfig>();
                    _current.hideFlags = HideFlags.HideAndDontSave;
                }

                return _current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _current = null;
        }
    }
}
