////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Martin Bustos @FronkonGames <fronkongames@gmail.com>. All rights reserved.
//
// THIS FILE CAN NOT BE HOSTED IN PUBLIC REPOSITORIES.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;
using UnityEditor;
using static FronkonGames.Artistic.TiltShift.Inspector;

namespace FronkonGames.Artistic.TiltShift.Editor
{
  /// <summary> Artistic Tilt Shift inspector. </summary>
  [CustomPropertyDrawer(typeof(TiltShift.Settings))]
  public class TiltShiftFeatureSettingsDrawer : Drawer
  {
    private TiltShift.Settings settings;

    protected override void ResetValues() => settings?.ResetDefaultValues();

    protected override void InspectorGUI()
    {
      settings ??= GetSettings<TiltShift.Settings>();

      /////////////////////////////////////////////////
      // Common.
      /////////////////////////////////////////////////
      settings.intensity = Slider("Intensity", "Controls the intensity of the effect [0, 1]. Default 0.", settings.intensity, 0.0f, 1.0f, 1.0f);

      /////////////////////////////////////////////////
      // Tilt Shift.
      /////////////////////////////////////////////////
      Separator();

      settings.quality = (TiltShift.Quality)EnumPopup("Quality", "Quality modes. Default High.", settings.quality, TiltShift.Quality.High);

      settings.angle = Slider("Angle", "Mask angle [-89, 90].", settings.angle, -90.0f, 90.0f, 0.0f);
      settings.aperture = Slider("Aperture", "Mask aperture [0.1, 5].", settings.aperture, 0.1f, 5.0f, 0.5f);
      settings.offset = Slider("Offset", "Mask vertical offset [-1.5, 1.5].", settings.offset, -1.5f, 1.5f, 0.0f);

      settings.blur = Slider("Blur strength", "Blur strength [0, 10].", settings.blur, 0.0f, 10.0f, 1.0f);
      IndentLevel++;
      settings.blurCurve = Slider("Curve", "Blur curve [1, 10].", settings.blurCurve, 1.0f, 10.0f, 3.0f);
      IndentLevel--;

      settings.distortion = Slider("Distortion", "Distortion strength [0, 20].", settings.distortion, 0.0f, 20.0f, 5.0f);
      IndentLevel++;
      settings.distortionScale = Slider("Scale", "Distortion scale [0.01, 2].", settings.distortionScale, 0.01f, 20.0f, 1.0f);
      IndentLevel--;

      Label("Focused zone");
      IndentLevel++;
      settings.focusedBrightness = Slider("Brightness", "Brightness [-1.0, 1.0]. Default 0.", settings.focusedBrightness, -1.0f, 1.0f, 0.0f);
      settings.focusedContrast = Slider("Contrast", "Contrast [0.0, 10.0]. Default 1.", settings.focusedContrast, 0.0f, 10.0f, 1.0f);
      settings.focusedGamma = Slider("Gamma", "Gamma [0.1, 10.0]. Default 1.", settings.focusedGamma, 0.01f, 10.0f, 1.0f);
      settings.focusedHue = Slider("Hue", "The color wheel [0.0, 1.0]. Default 0.", settings.focusedHue, 0.0f, 1.0f, 0.0f);
      settings.focusedSaturation = Slider("Saturation", "Intensity of a colors [0.0, 2.0]. Default 1.", settings.focusedSaturation, 0.0f, 2.0f, 1.0f);
      IndentLevel--;

      Label("Unfocused zone");
      IndentLevel++;
      settings.unfocusedBrightness = Slider("Brightness", "Brightness [-1.0, 1.0]. Default 0.", settings.unfocusedBrightness, -1.0f, 1.0f, 0.0f);
      settings.unfocusedContrast = Slider("Contrast", "Contrast [0.0, 10.0]. Default 1.", settings.unfocusedContrast, 0.0f, 10.0f, 1.0f);
      settings.unfocusedGamma = Slider("Gamma", "Gamma [0.1, 10.0]. Default 1.", settings.unfocusedGamma, 0.01f, 10.0f, 1.0f);
      settings.unfocusedHue = Slider("Hue", "The color wheel [0.0, 1.0]. Default 0.", settings.unfocusedHue, 0.0f, 1.0f, 0.0f);
      settings.unfocusedSaturation = Slider("Saturation", "Intensity of a colors [0.0, 2.0]. Default 1.", settings.unfocusedSaturation, 0.0f, 2.0f, 1.0f);
      IndentLevel--;

      settings.debugView = Toggle("Debug", "Debug view, only available in the Editor.", settings.debugView);

      /////////////////////////////////////////////////
      // Color.
      /////////////////////////////////////////////////
      Separator();

      if (Foldout("Color") == true)
      {
        IndentLevel++;

        settings.brightness = Slider("Brightness", "Brightness [-1.0, 1.0]. Default 0.", settings.brightness, -1.0f, 1.0f, 0.0f);
        settings.contrast = Slider("Contrast", "Contrast [0.0, 10.0]. Default 1.", settings.contrast, 0.0f, 10.0f, 1.0f);
        settings.gamma = Slider("Gamma", "Gamma [0.1, 10.0]. Default 1.", settings.gamma, 0.01f, 10.0f, 1.0f);
        settings.hue = Slider("Hue", "The color wheel [0.0, 1.0]. Default 0.", settings.hue, 0.0f, 1.0f, 0.0f);
        settings.saturation = Slider("Saturation", "Intensity of a colors [0.0, 2.0]. Default 1.", settings.saturation, 0.0f, 2.0f, 1.0f);

        IndentLevel--;
      }

      /////////////////////////////////////////////////
      // Advanced.
      /////////////////////////////////////////////////
      Separator();

      if (Foldout("Advanced") == true)
      {
        IndentLevel++;

#if !UNITY_6000_0_OR_NEWER
        settings.filterMode = (FilterMode)EnumPopup("Filter mode", "Filter mode. Default Bilinear.", settings.filterMode, FilterMode.Bilinear);
#endif
        settings.affectSceneView = Toggle("Affect the Scene View?", "Does it affect the Scene View?", settings.affectSceneView);
        settings.whenToInsert = (UnityEngine.Rendering.Universal.RenderPassEvent)EnumPopup("RenderPass event",
          "Render pass injection. Default BeforeRenderingTransparents.",
          settings.whenToInsert,
          UnityEngine.Rendering.Universal.RenderPassEvent.BeforeRenderingTransparents);
#if !UNITY_6000_0_OR_NEWER
        settings.enableProfiling = Toggle("Enable profiling", "Enable render pass profiling", settings.enableProfiling);
#endif

        IndentLevel--;
      }
    }
  }
}
