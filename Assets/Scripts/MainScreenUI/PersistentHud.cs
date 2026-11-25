using UnityEngine;

namespace Mousword.MainScreenUI
{
    /// <summary>
    /// Keeps the HUD alive across stage transitions (like the player, camera and GameManager).
    /// A HUD placed in a later scene destroys itself when one is already carried over.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class PersistentHud : MonoBehaviour
    {
        private static PersistentHud instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                // Hide immediately so the duplicate never renders for a frame before Destroy completes.
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
