using UnityEngine;

namespace AnimeAssistant.Presentation
{
    [DisallowMultipleComponent]
    public sealed class SceneEntryPoint : MonoBehaviour
    {
        [SerializeField] private string sceneRole = "Prototype";

        public string SceneRole => sceneRole;
    }
}

