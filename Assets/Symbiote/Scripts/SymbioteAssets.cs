using UnityEngine;

namespace SymbioteRaid
{
    [CreateAssetMenu(menuName = "Symbiote/Recursos del prototipo")]
    public sealed class SymbioteAssets : ScriptableObject
    {
        public GameObject swatModel;
        public GameObject symbioteModel, symbioteCoreModel, symbioteTentacleModel;
        public RuntimeAnimatorController swatAnimations;
        public Texture2D bodyDiffuse, headDiffuse;
        public Material slimeMaterial, infectionMaterial;
        public Material wallMaterial, floorMaterial, metalMaterial, accentMaterial;
    }
}
