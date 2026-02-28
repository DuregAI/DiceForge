using System;
using UnityEngine;

[Obsolete("Legacy GameBootstrap must not be used. Use BattleLauncher + BattleSceneBootstrapper strict pipeline.", true)]
public class GameBootstrap : MonoBehaviour
{
    private void Start()
    {
        throw new InvalidOperationException("[GameBootstrap] Component is forbidden in strict battle pipeline. Remove GameBootstrap from the Battle scene.");
    }
}
