using System;
using System.Collections.Generic;
using DraftSight.Interop.dsAutomation;

namespace CanvasCovers.Geometry
{
    // Manages named layers and entity-to-layer assignment via the SDK's
    // verified "activate-based" pattern:
    //   1. Save the currently active layer.
    //   2. Activate the target layer.
    //   3. Insert the entity (it lands on the active layer at creation).
    //   4. Restore the originally active layer when finished.
    //
    // We deliberately do NOT use EntityHelper.SetLayer / GetLayer — both
    // crash DraftSight when called on freshly-inserted entities. See
    // CLAUDE.md §9 for the verification trail.
    public sealed class LayerHelper : IDisposable
    {
        private readonly LayerManager _layerManager;
        private readonly Layer _originalActive;
        private readonly Dictionary<string, Layer> _layers = new Dictionary<string, Layer>(StringComparer.OrdinalIgnoreCase);
        private string _currentActiveName;
        private bool _disposed;

        public LayerHelper(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            _layerManager = document.GetLayerManager()
                ?? throw new InvalidOperationException("DraftSight did not return a layer manager.");

            _originalActive = _layerManager.GetActiveLayer();
            _currentActiveName = _originalActive?.Name;
        }

        // Ensure the named layer exists and is tracked. Best-effort color
        // assignment: if the COM call to set the color fails, we swallow it
        // — the layer still exists, just with the default color.
        public Layer EnsureLayer(string name, int? colorIndex = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            if (_layers.TryGetValue(name, out Layer existing))
            {
                return existing;
            }

            Layer layer;
            dsCreateObjectResult_e result;
            _layerManager.CreateLayer(name, out layer, out result);

            if (layer == null)
            {
                // CreateLayer returned null (e.g. AlreadyExists in some hosts).
                // Look it up explicitly.
                layer = _layerManager.GetLayer(name);
            }

            if (layer == null)
            {
                throw new InvalidOperationException("Could not create or fetch layer '" + name + "'. Result: " + result);
            }

            if (colorIndex.HasValue)
            {
                TrySetColor(layer, colorIndex.Value);
            }

            _layers[name] = layer;
            return layer;
        }

        // Make the named layer active so the next entity insert lands on it.
        // No-op if already active.
        public void Activate(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (string.Equals(_currentActiveName, name, StringComparison.OrdinalIgnoreCase)) return;

            if (!_layers.TryGetValue(name, out Layer layer))
            {
                throw new InvalidOperationException("Layer '" + name + "' was not registered via EnsureLayer.");
            }

            if (!layer.Activate())
            {
                throw new InvalidOperationException("Could not activate layer '" + name + "'.");
            }
            _currentActiveName = name;
        }

        // Restore whichever layer was active when this helper was constructed.
        // Safe to call multiple times; safe even if no original active was found.
        public void RestoreOriginalActive()
        {
            if (_originalActive == null) return;
            if (string.Equals(_currentActiveName, _originalActive.Name, StringComparison.OrdinalIgnoreCase)) return;

            if (_originalActive.Activate())
            {
                _currentActiveName = _originalActive.Name;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { RestoreOriginalActive(); } catch { /* best effort */ }
        }

        private static void TrySetColor(Layer layer, int colorIndex)
        {
            try
            {
                Color color = layer.Color;
                if (color != null)
                {
                    color.SetColorByIndex(colorIndex);
                    layer.Color = color;
                }
            }
            catch
            {
                // best effort — a missing color is cosmetic only
            }
        }
    }
}
