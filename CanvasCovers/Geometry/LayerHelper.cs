using System;
using DraftSight.Interop.dsAutomation;

namespace CanvasCovers.Geometry
{
    public static class LayerNames
    {
        public const string Outline = "CC-Outline";
        public const string Cop = "CC-COP";
        public const string Annotation = "CC-Annotation";
        public const string Titleblock = "CC-Titleblock";
    }

    public class LayerHelper
    {
        private readonly Document _document;
        private readonly LayerManager _layerManager;
        private readonly EntityHelper _entityHelper;

        public LayerHelper(Document document, EntityHelper entityHelper)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _entityHelper = entityHelper ?? throw new ArgumentNullException(nameof(entityHelper));

            _layerManager = document.GetLayerManager();
            if (_layerManager == null)
            {
                throw new InvalidOperationException("DraftSight did not return a layer manager for the active document.");
            }
        }

        public void EnsureLayer(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            _layerManager.CreateLayer(name, out Layer _, out dsCreateObjectResult_e _);
            // dsCreateObjectResult_Error and dsCreateObjectResult_AlreadyExists are both fine for our purposes:
            // we only care that the layer exists after this call.
        }

        public void SetLayer(object entity, string layerName)
        {
            if (entity == null || string.IsNullOrEmpty(layerName)) return;
            _entityHelper.SetLayer(entity, layerName);
        }
    }
}
