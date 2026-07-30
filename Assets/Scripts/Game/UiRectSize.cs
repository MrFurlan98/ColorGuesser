using UnityEngine;
using UnityEngine.UI;

namespace ColorGuesser.Game
{
    /// <summary>
    /// Feeds this UI element's pixel size into its material, so a Shader Graph can do
    /// size-aware effects (procedural rounded corners, borders, uniform glow) that do
    /// not stretch with the element's aspect ratio.
    ///
    /// Setup:
    ///   1. In the Shader Graph add an exposed Vector2 property whose Reference is
    ///      "_RectSize" (or change sizeProperty below to match yours).
    ///   2. Use it with UV0 to build the effect, e.g. corner radius in pixels.
    ///   3. Set the Image's type to Simple (procedural rounding does not need 9-slice).
    ///   4. Put this component on the same object as the Image.
    ///
    /// Note: this gives the element its OWN material instance, which breaks uGUI
    /// batching for it. Fine for a handful of panels/buttons - do NOT use it on the
    /// 480 board cells (use a shared material and the sprite's alpha instead).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UiRectSize : MonoBehaviour, IMaterialModifier
    {
        [Tooltip("Shader property reference that receives (width, height) in pixels.")]
        [SerializeField] private string sizeProperty = "_RectSize";

        [Tooltip("Shader property reference that receives the rect's local centre.")]
        [SerializeField] private string centerProperty = "_RectCenter";

        private Graphic _graphic;
        private Material _instance;

        private Graphic Graphic => _graphic != null ? _graphic : (_graphic = GetComponent<Graphic>());

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (baseMaterial == null) return null;

            // Reuse one instance per element; recreate it if the base shader changed.
            if (_instance == null || _instance.shader != baseMaterial.shader)
            {
                if (_instance != null) DestroyInstance();
                _instance = new Material(baseMaterial) { name = baseMaterial.name + " (rect size)" };
            }

            var r = ((RectTransform)transform).rect;
            if (_instance.HasProperty(sizeProperty))
                _instance.SetVector(sizeProperty, new Vector4(r.width, r.height, 0f, 0f));
            // The rect's centre in local space, so the shader can work from the element's
            // own geometry instead of sprite UVs (which are a sub-rect when atlased).
            if (_instance.HasProperty(centerProperty))
                _instance.SetVector(centerProperty, new Vector4(r.center.x, r.center.y, 0f, 0f));
            return _instance;
        }

        // Re-push the size whenever the element is laid out or resized.
        private void OnRectTransformDimensionsChange()
        {
            if (Graphic != null) Graphic.SetMaterialDirty();
        }

        private void OnEnable()
        {
            if (Graphic != null) Graphic.SetMaterialDirty();
        }

        private void OnDisable() => DestroyInstance();
        private void OnDestroy() => DestroyInstance();

        private void DestroyInstance()
        {
            if (_instance == null) return;
            if (Application.isPlaying) Destroy(_instance);
            else DestroyImmediate(_instance);
            _instance = null;
        }
    }
}
