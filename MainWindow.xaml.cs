using MadFractal.Rendering;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

namespace MadFractal
{
    public sealed partial class MainWindow : Window
    {
        private FractalRenderer? _renderer;
        private bool _isInitializing = true;
        private bool _isDraggingThumb;
        private bool _isDraggingJuliaFromWindow;
        private bool _isPanningFractal;
        private Point _lastFractalPanPosition;
        private float _juliaX;
        private float _juliaY;
        private float _escapeRadius = 100;
        private uint _power = 2;
        private uint _maxIterations = 100;
        private uint _function = 1;
        private uint _coloringMethod = 0;
        private uint _useSmooth = 1;
        private uint _orbitTrapType = 4;
        private Vector3 _paletteBase = new(0.50f, 0.15f, 0.05f);
        private Vector3 _paletteAmplitude = new(0.50f, 0.35f, 0.25f);
        private Vector3 _paletteFrequency = new(1.00f, 0.80f, 0.50f);
        private Vector3 _palettePhase = new(0.00f, 0.05f, 0.10f);
        private readonly WriteableBitmap _hsvWheelBitmap = new(220, 220);
        private bool _isDraggingHsvWheel;
        private float _hsvHue = 13.333f;
        private float _hsvSaturation = 0.9f;
        private float _hsvBrightness = 0.5f;
        private bool _isSynchronizingColorControls;
        private AppWindow? _appWindow;

        private readonly record struct JuliaPreset(
            float X,
            float Y,
            uint Power,
            float EscapeRadius,
            uint MaxIterations,
            uint Function,
            uint ColoringMethod,
            uint UseSmooth,
            uint OrbitTrapType,
            Vector3 Base,
            Vector3 Amplitude,
            Vector3 Frequency,
            Vector3 Phase);

        private static readonly JuliaPreset[] Presets =
        {
            new(-0.12f, 0.74f, 2, 100, 180, 1, 0, 1, 4,
                new Vector3(0.38f, 0.12f, 0.55f), new Vector3(0.42f, 0.30f, 0.45f), new Vector3(0.85f, 0.70f, 1.05f), new Vector3(0.00f, 0.12f, 0.28f)),
            new(-0.75f, 0.11f, 3, 100, 220, 1, 2, 1, 3,
                new Vector3(0.03f, 0.12f, 0.28f), new Vector3(0.45f, 0.30f, 0.55f), new Vector3(0.70f, 0.95f, 1.15f), new Vector3(0.50f, 0.20f, 0.02f)),
            new(0.28f, 0.53f, 2, 100, 160, 1, 3, 1, 4,
                new Vector3(0.22f, 0.18f, 0.12f), new Vector3(0.52f, 0.38f, 0.28f), new Vector3(0.85f, 0.65f, 0.45f), new Vector3(0.02f, 0.16f, 0.30f)),
            new(-0.35f, -0.62f, 2, 100, 140, 3, 2, 1, 1,
                new Vector3(0.02f, 0.18f, 0.16f), new Vector3(0.22f, 0.46f, 0.42f), new Vector3(0.90f, 1.05f, 0.75f), new Vector3(0.18f, 0.42f, 0.58f)),
            new(0.37f, -0.21f, 2, 100, 140, 5, 3, 1, 4,
                new Vector3(0.34f, 0.25f, 0.12f), new Vector3(0.48f, 0.34f, 0.24f), new Vector3(0.85f, 0.65f, 0.45f), new Vector3(0.02f, 0.16f, 0.30f)),
            new(-0.54f, 0.18f, 4, 100, 240, 0, 2, 1, 5,
                new Vector3(0.12f, 0.04f, 0.25f), new Vector3(0.38f, 0.45f, 0.58f), new Vector3(1.15f, 0.90f, 0.70f), new Vector3(0.00f, 0.33f, 0.66f)),
            new(0.00f, 0.00f, 2, 100, 130, 2, 2, 1, 0,
                new Vector3(0.26f, 0.08f, 0.04f), new Vector3(0.52f, 0.32f, 0.20f), new Vector3(0.65f, 0.90f, 1.20f), new Vector3(0.00f, 0.16f, 0.34f)),
            new(-0.21f, 0.67f, 2, 100, 160, 4, 0, 1, 4,
                new Vector3(0.08f, 0.16f, 0.36f), new Vector3(0.34f, 0.38f, 0.50f), new Vector3(0.80f, 1.00f, 0.70f), new Vector3(0.12f, 0.38f, 0.62f)),
            new(0.10f, -0.74f, 2, 100, 150, 3, 2, 1, 6,
                new Vector3(0.25f, 0.05f, 0.18f), new Vector3(0.50f, 0.32f, 0.44f), new Vector3(1.00f, 0.80f, 0.60f), new Vector3(0.00f, 0.22f, 0.48f)),
            new(-0.16f, 0.65f, 2, 100, 170, 5, 0, 1, 4,
                new Vector3(0.20f, 0.24f, 0.28f), new Vector3(0.42f, 0.40f, 0.36f), new Vector3(0.75f, 0.90f, 1.05f), new Vector3(0.08f, 0.20f, 0.36f))
        };

        public MainWindow()
        {
            InitializeComponent();
            var windowHandle = WindowNative.GetWindowHandle(this);
            var windowId = new WindowId((ulong)windowHandle.ToInt64());
            _appWindow = AppWindow.GetFromWindowId(windowId);
            HsvWheelImage.Source = _hsvWheelBitmap;
            RenderHsvWheel();
            UpdateHsvThumb();
            UpdateSelectedColorPreview();
            SetJuliaThumbPosition(XYPad.Width / 2, XYPad.Height / 2);
            _isInitializing = false;
            UpdateFractalInfo();
            FractalPanel.Loaded += (_, _) =>
            {
                if (_renderer is null)
                {
                    _renderer = new FractalRenderer(FractalPanel);
                    ApplyParameters();
                }

            };
            FractalPanel.SizeChanged += (_, _) =>
            {
                if (_renderer is null || FractalPanel.XamlRoot is null)
                    return;

                var scale = FractalPanel.XamlRoot.RasterizationScale;
                var width = Math.Max(1u, (uint)Math.Round(FractalPanel.ActualWidth * scale));
                var height = Math.Max(1u, (uint)Math.Round(FractalPanel.ActualHeight * scale));
                _renderer.Resize(width, height);
            };
            Closed += (_, _) =>
            {
                _renderer?.Dispose();
                _renderer = null;
            };
        }

        private void ParameterSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isInitializing || _isSynchronizingColorControls)//защита от зацикливания ползунков и HSV
                return;

            if (sender is not Slider slider || slider.Tag is not string parameter)
                return;

            var value = e.NewValue;
            UpdateParameterText(slider, parameter, value);
            switch (parameter)
            {
                case "PanelOpacity":
                    ControlPanel.Opacity = value;
                    return;
                case "Power":
                    _power = (uint)Math.Round(value);
                    PowerValue.Text = _power.ToString();
                    break;
                case "EscapeRadius":
                    _escapeRadius = (float)value;
                    EscapeRadiusValue.Text = _escapeRadius.ToString("0.00");
                    break;
                case "MaxIterations":
                    _maxIterations = (uint)Math.Round(value);
                    MaxIterationsValue.Text = _maxIterations.ToString();
                    break;
                case "Brightness":
                    _hsvBrightness = (float)value;
                    BrightnessValue.Text = _hsvBrightness.ToString("0.00");
                    RenderHsvWheel();
                    _paletteBase = HsvToRgb(_hsvHue, _hsvSaturation, _hsvBrightness);
                    UpdateBaseSliders();
                    UpdateSelectedColorPreview();
                    break;
                default:
                    UpdatePaletteValue(parameter, parameter is "BaseR" or "BaseG" or "BaseB"//если RGB база то переводим обратно в [0;1] иначе оставляем как есть
                        ? (float)value / 255f
                        : (float)value);
                    if (parameter is "BaseR" or "BaseG" or "BaseB")
                        UpdateHsvFromPaletteBase();
                    break;
            }

            ApplyParameters();
        }

        private void ParameterTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter)
                return;

            ApplyParameterTextBoxValue(sender);
            e.Handled = true;
        }

        private void ParameterTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyParameterTextBoxValue(sender);
        }

        private void ApplyParameterTextBoxValue(object sender)
        {
            if (sender is not TextBox textBox || textBox.Tag is not string parameter)
                return;

            var slider = FindParameterSlider(textBox, parameter);
            if (slider is null)
                return;

            if (!TryParseParameterValue(textBox.Text, out var value))
            {
                textBox.Text = FormatParameterValue(parameter, slider.Value);
                return;
            }

            if (parameter is "Power" or "MaxIterations" or "BaseR" or "BaseG" or "BaseB")
                value = Math.Round(value);

            value = Math.Clamp(value, slider.Minimum, slider.Maximum);
            slider.Value = value;
            textBox.Text = FormatParameterValue(parameter, slider.Value);
        }

        private static bool TryParseParameterValue(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
                double.IsFinite(value))
            {
                return true;
            }

            return double.TryParse(
                text.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value) && double.IsFinite(value);
        }

        private static Slider? FindParameterSlider(TextBox textBox, string parameter)
        {
            if (textBox.Parent is not Grid grid)
                return null;

            foreach (var child in grid.Children)
            {
                if (child is Slider slider && slider.Tag as string == parameter)
                    return slider;
            }

            return null;
        }

        private static void UpdateParameterText(Slider slider, string parameter, double value)
        {
            if (slider.Parent is not Grid grid)
                return;

            foreach (var child in grid.Children)
            {
                if (child is TextBox textBox && textBox.Tag as string == parameter)
                    textBox.Text = FormatParameterValue(parameter, value);
            }
        }

        private static string FormatParameterValue(string parameter, double value)
        {
            return parameter is "Power" or "MaxIterations" or "BaseR" or "BaseG" or "BaseB"
                ? Math.Round(value).ToString(CultureInfo.CurrentCulture)
                : value.ToString("0.00", CultureInfo.CurrentCulture);
        }

        private void HsvWheel_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingHsvWheel = true;
            HsvWheel.CapturePointer(e.Pointer);
            UpdateHsvSelection(e.GetCurrentPoint(HsvWheel).Position);
            e.Handled = true;
        }

        private void HsvWheel_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingHsvWheel)
                return;

            UpdateHsvSelection(e.GetCurrentPoint(HsvWheel).Position);
            e.Handled = true;
        }

        private void HsvWheel_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            StopHsvWheelDragging(e.Pointer);
            e.Handled = true;
        }

        private void HsvWheel_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            StopHsvWheelDragging(e.Pointer);
            e.Handled = true;
        }

        private void HsvWheel_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingHsvWheel = false;
        }

        private void StopHsvWheelDragging(Pointer pointer)
        {
            _isDraggingHsvWheel = false;
            HsvWheel.ReleasePointerCapture(pointer);
        }

        private void UpdateHsvSelection(Point position)
        {
            const float center = 110f;
            const float maxRadius = 101f;

            float dx = (float)position.X - center;//относительно центра
            float dy = (float)position.Y - center;
            float distance = MathF.Sqrt(dx * dx + dy * dy);

            if (distance > maxRadius)//не позволяет выйти за пределы
            {
                float scale = maxRadius / distance;
                dx *= scale;
                dy *= scale;
                distance = maxRadius;
            }

            _hsvHue = MathF.Atan2(dy, dx) * 180f / MathF.PI;//формула оттенка(atan2 - уголь точки относительно центра), сразу переводится в градусы
            if (_hsvHue < 0)
                _hsvHue += 360f;

            _hsvSaturation = distance / maxRadius;//насыщенность(дальше - больше)
            UpdateHsvThumb();
            _paletteBase = HsvToRgb(_hsvHue, _hsvSaturation, _hsvBrightness);
            UpdateBaseSliders();
            UpdateSelectedColorPreview();
            ApplyParameters();
        }

        private void UpdateHsvThumb()//перемещает указатель в кругу(переводит HSV-корды обратно в Canvas-корды)
        {
            const float center = 110f;
            const float maxRadius = 101f;
            const float thumbRadius = 9f;

            float angle = _hsvHue * MathF.PI / 180f;
            float distance = _hsvSaturation * maxRadius;
            float x = center + MathF.Cos(angle) * distance;
            float y = center + MathF.Sin(angle) * distance;

            Canvas.SetLeft(HsvWheelThumb, x - thumbRadius);//вычитания просто чтобы корды указывали на центр круга а не на угол
            Canvas.SetTop(HsvWheelThumb, y - thumbRadius);
        }

        private void RenderHsvWheel()//генерирует сам разноцветный HSV круг
        {
            const int size = 220;
            const float center = 110f;
            const float maxRadius = 101f;
            byte[] pixels = new byte[size * size * 4];//4 байта на пиксель - RGBA 

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float distance = MathF.Sqrt(dx * dx + dy * dy);
                    int offset = (y * size + x) * 4;//смещение в массиве битмапа для конкретного пикселя

                    if (distance > maxRadius)//Отбрасывает пиксели за пределами круга(прозрачные)
                        continue;

                    float hue = MathF.Atan2(dy, dx) * 180f / MathF.PI;//считает HSV для конкретного пикселя
                    if (hue < 0)
                        hue += 360f;

                    Vector3 color = HsvToRgb(hue, distance / maxRadius, _hsvBrightness);
                    pixels[offset] = ToByte(color.Z);//особенный порядок у битмапа B,G,R,A
                    pixels[offset + 1] = ToByte(color.Y);
                    pixels[offset + 2] = ToByte(color.X);
                    pixels[offset + 3] = 255;
                }
            }

            using Stream stream = _hsvWheelBitmap.PixelBuffer.AsStream();
            stream.Position = 0;
            stream.Write(pixels, 0, pixels.Length);
            _hsvWheelBitmap.Invalidate();
        }

        private void UpdateSelectedColorPreview()//отображает цвет в прямоугольнике
        {
            byte red = ToByte(_paletteBase.X);//переводит цвета в 255
            byte green = ToByte(_paletteBase.Y);
            byte blue = ToByte(_paletteBase.Z);
            SelectedColorPreview.Background = new SolidColorBrush(Color.FromArgb(255, red, green, blue));
        }

        private static byte ToByte(float value)
        {
            return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);//переводит из [0;1] в [0;255] 
        }

        private static Vector3 HsvToRgb(float hue, float saturation, float value)//hue - оттенок, saturation - насыщенность, value - яркость
        {
            hue = (hue % 360f + 360f) % 360f;//гарант того что в [0;360]
            float h = hue / 60f;//деление на 6 частей
            float c = value * saturation;//дальше пофиг чистые формулы
            float x = c * (1f - MathF.Abs(h % 2f - 1f));
            float m = value - c;

            Vector3 rgb = h switch
            {
                < 1f => new Vector3(c, x, 0),
                < 2f => new Vector3(x, c, 0),
                < 3f => new Vector3(0, c, x),
                < 4f => new Vector3(0, x, c),
                < 5f => new Vector3(x, 0, c),
                _ => new Vector3(c, 0, x)
            };

            return rgb + new Vector3(m);
        }

        private void UpdateBaseSliders()//синхронит ползунки с текущим цветом
        {
            _isSynchronizingColorControls = true;//защита от зацикливания ползунков и HSV(в методе valueChanged происходит проверка)
            try// try finally нужен, чтобы флаг гарантированно сбросился даже при возникновении исключения. Иначе после ошибки события слайдеров могли бы навсегда перестать обрабатываться.
            {
                BaseRSlider.Value = ToByte(_paletteBase.X);
                BaseGSlider.Value = ToByte(_paletteBase.Y);
                BaseBSlider.Value = ToByte(_paletteBase.Z);
                BaseRValue.Text = ToByte(_paletteBase.X).ToString();
                BaseGValue.Text = ToByte(_paletteBase.Y).ToString();
                BaseBValue.Text = ToByte(_paletteBase.Z).ToString();
            }
            finally
            {
                _isSynchronizingColorControls = false;
            }
        }

        private void UpdateHsvFromPaletteBase()//обратный перевод в HSV из RGB чтобы засинхронить ползунки и круг
        {
            float max = MathF.Max(_paletteBase.X, MathF.Max(_paletteBase.Y, _paletteBase.Z));
            float min = MathF.Min(_paletteBase.X, MathF.Min(_paletteBase.Y, _paletteBase.Z));
            float delta = max - min;

            _hsvBrightness = max;
            _hsvSaturation = max <= 0f ? 0f : delta / max;

            if (delta > 0f)
            {
                if (max == _paletteBase.X)
                    _hsvHue = 60f * ((_paletteBase.Y - _paletteBase.Z) / delta % 6f);
                else if (max == _paletteBase.Y)
                    _hsvHue = 60f * ((_paletteBase.Z - _paletteBase.X) / delta + 2f);
                else
                    _hsvHue = 60f * ((_paletteBase.X - _paletteBase.Y) / delta + 4f);

                if (_hsvHue < 0f)
                    _hsvHue += 360f;
            }

            _isSynchronizingColorControls = true;
            try
            {
                BrightnessSlider.Value = _hsvBrightness;
                BrightnessValue.Text = _hsvBrightness.ToString("0.00");
            }
            finally
            {
                _isSynchronizingColorControls = false;
            }

            RenderHsvWheel();
            UpdateHsvThumb();
            UpdateSelectedColorPreview();
        }

        private void FunctionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || FunctionSelector.SelectedIndex < 0)
                return;

            _function = (uint)FunctionSelector.SelectedIndex;
            JuliaConstantPanel.Visibility = _function is 1 or 3 or 5
                ? Visibility.Visible
                : Visibility.Collapsed;
            ApplyParameters();
        }

        private void XYPad_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(XYPad).Properties.IsRightButtonPressed)
                return;

            _isDraggingThumb = true;
            XYPad.CapturePointer(e.Pointer);
            var position = e.GetCurrentPoint(XYPad).Position;
            SetJuliaThumbPosition(position.X, position.Y);
            e.Handled = true;
        }

        private void XYPad_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingThumb)
                return;

            var position = e.GetCurrentPoint(XYPad).Position;
            SetJuliaThumbPosition(position.X, position.Y);
            e.Handled = true;
        }

        private void XYPad_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            StopDragging(e.Pointer);
            e.Handled = true;
        }

        private void XYPad_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            StopDragging(e.Pointer);
            e.Handled = true;
        }

        private void XYPad_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingThumb = false;
        }

        private void StopDragging(Pointer pointer)
        {
            _isDraggingThumb = false;
            XYPad.ReleasePointerCapture(pointer);
        }

        private void FractalPanel_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(FractalPanel);
            if (!point.Properties.IsMiddleButtonPressed)
                return;

            _isPanningFractal = true;
            _lastFractalPanPosition = point.Position;
            FractalPanel.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void FractalPanel_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isPanningFractal || FractalPanel.ActualWidth <= 0 || FractalPanel.ActualHeight <= 0)
                return;

            var position = e.GetCurrentPoint(FractalPanel).Position;
            var delta = new Vector2(
                (float)((position.X - _lastFractalPanPosition.X) / FractalPanel.ActualWidth),
                (float)((position.Y - _lastFractalPanPosition.Y) / FractalPanel.ActualHeight));
            _lastFractalPanPosition = position;
            _renderer?.Pan(delta);
            e.Handled = true;
        }

        private void FractalPanel_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            StopFractalPanning(e.Pointer);
            e.Handled = true;
        }

        private void FractalPanel_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            StopFractalPanning(e.Pointer);
            e.Handled = true;
        }

        private void FractalPanel_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isPanningFractal = false;
        }

        private void FractalPanel_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (FractalPanel.ActualWidth <= 0 || FractalPanel.ActualHeight <= 0)
                return;

            var point = e.GetCurrentPoint(FractalPanel);
            var position = new Vector2(
                (float)(point.Position.X / FractalPanel.ActualWidth),
                (float)(point.Position.Y / FractalPanel.ActualHeight));
            _renderer?.ZoomAt(position, point.Properties.MouseWheelDelta);
            UpdateFractalInfo();
            e.Handled = true;
        }

        private void StopFractalPanning(Pointer pointer)
        {
            if (!_isPanningFractal)
                return;

            _isPanningFractal = false;
            FractalPanel.ReleasePointerCapture(pointer);
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint(RootGrid).Properties.IsRightButtonPressed)
                return;

            _isDraggingJuliaFromWindow = true;
            RootGrid.CapturePointer(e.Pointer);
            SetJuliaFromWindowPosition(e.GetCurrentPoint(FractalPanel).Position);
            e.Handled = true;
        }

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingJuliaFromWindow)
                return;

            SetJuliaFromWindowPosition(e.GetCurrentPoint(FractalPanel).Position);
            e.Handled = true;
        }

        private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingJuliaFromWindow)
                return;

            StopWindowDragging(e.Pointer);
            e.Handled = true;
        }

        private void RootGrid_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingJuliaFromWindow)
                return;

            StopWindowDragging(e.Pointer);
            e.Handled = true;
        }

        private void RootGrid_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingJuliaFromWindow = false;
        }

        private void StopWindowDragging(Pointer pointer)
        {
            _isDraggingJuliaFromWindow = false;
            RootGrid.ReleasePointerCapture(pointer);
        }

        private void SetJuliaFromWindowPosition(Point position)
        {
            if (FractalPanel.ActualWidth <= 0 || FractalPanel.ActualHeight <= 0)
                return;

            const double thumbRadius = 10;
            var x = thumbRadius + Math.Clamp(position.X / FractalPanel.ActualWidth, 0, 1)
                * (XYPad.Width - 2 * thumbRadius);
            var y = thumbRadius + Math.Clamp(position.Y / FractalPanel.ActualHeight, 0, 1)
                * (XYPad.Height - 2 * thumbRadius);
            SetJuliaThumbPosition(x, y);
        }

        private void SetJuliaThumbPosition(double x, double y)
        {
            const double thumbRadius = 10;

            x = Math.Clamp(x, thumbRadius, XYPad.Width - thumbRadius);
            y = Math.Clamp(y, thumbRadius, XYPad.Height - thumbRadius);

            Canvas.SetLeft(Thumb, x - thumbRadius);
            Canvas.SetTop(Thumb, y - thumbRadius);

            _juliaX = (float)(2 * (x - thumbRadius) / (XYPad.Width - 2 * thumbRadius) - 1);
            _juliaY = (float)(2 * (1.0 - (y - thumbRadius) / (XYPad.Height - 2 * thumbRadius)) - 1);
            JuliaConstantValue.Text = $"c = {_juliaX:0.00} {(_juliaY >= 0 ? "+" : "-")} {MathF.Abs(_juliaY):0.00}i";

            _renderer?.UpdateJuliaConstant(_juliaX, _juliaY);
        }

        private void ColoringMethodSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || ColoringMethodSelector.SelectedIndex < 0)
                return;

            _coloringMethod = (uint)ColoringMethodSelector.SelectedIndex;

            bool isMonochrome = (_coloringMethod == 1);
            PalettePanel.IsHitTestVisible = !isMonochrome;
            PalettePanel.Opacity = isMonochrome ? 0.3 : 1.0;
            bool usesEscapeTimeSmoothing = _coloringMethod == 0;
            SmoothingMethodSelector.IsEnabled = usesEscapeTimeSmoothing;
            SmoothingMethodSelector.Opacity = usesEscapeTimeSmoothing ? 1.0 : 0.4;
            ApplyParameters();
        }

        private void SmoothingMethodSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || SmoothingMethodSelector.SelectedIndex < 0)
                return;

            _useSmooth = (uint)SmoothingMethodSelector.SelectedIndex;
            ApplyParameters();
        }

        private void OrbitTrapTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || OrbitTrapTypeSelector.SelectedIndex < 0)
                return;

            _orbitTrapType = (uint)OrbitTrapTypeSelector.SelectedIndex;
            ApplyParameters();
        }

        private void UpdatePaletteValue(string parameter, float value)
        {
            switch (parameter)
            {
                case "BaseR": _paletteBase.X = value; BaseRValue.Text = ToByte(value).ToString(); break;
                case "BaseG": _paletteBase.Y = value; BaseGValue.Text = ToByte(value).ToString(); break;
                case "BaseB": _paletteBase.Z = value; BaseBValue.Text = ToByte(value).ToString(); break;
                case "AmplitudeR": _paletteAmplitude.X = value; AmplitudeRValue.Text = value.ToString("0.00"); break;
                case "AmplitudeG": _paletteAmplitude.Y = value; AmplitudeGValue.Text = value.ToString("0.00"); break;
                case "AmplitudeB": _paletteAmplitude.Z = value; AmplitudeBValue.Text = value.ToString("0.00"); break;
                case "FrequencyR": _paletteFrequency.X = value; FrequencyRValue.Text = value.ToString("0.00"); break;
                case "FrequencyG": _paletteFrequency.Y = value; FrequencyGValue.Text = value.ToString("0.00"); break;
                case "FrequencyB": _paletteFrequency.Z = value; FrequencyBValue.Text = value.ToString("0.00"); break;
                case "PhaseR": _palettePhase.X = value; PhaseRValue.Text = value.ToString("0.00"); break;
                case "PhaseG": _palettePhase.Y = value; PhaseGValue.Text = value.ToString("0.00"); break;
                case "PhaseB": _palettePhase.Z = value; PhaseBValue.Text = value.ToString("0.00"); break;
            }
        }

        private void UpdatePaletteSliders()
        {
            _isSynchronizingColorControls = true;
            try
            {
                foreach (var slider in FindSliders(PalettePanel))
                {
                    if (slider.Tag is not string parameter)
                        continue;

                    var value = parameter switch
                    {
                        "BaseR" => _paletteBase.X * 255f,
                        "BaseG" => _paletteBase.Y * 255f,
                        "BaseB" => _paletteBase.Z * 255f,
                        "AmplitudeR" => _paletteAmplitude.X,
                        "AmplitudeG" => _paletteAmplitude.Y,
                        "AmplitudeB" => _paletteAmplitude.Z,
                        "FrequencyR" => _paletteFrequency.X,
                        "FrequencyG" => _paletteFrequency.Y,
                        "FrequencyB" => _paletteFrequency.Z,
                        "PhaseR" => _palettePhase.X,
                        "PhaseG" => _palettePhase.Y,
                        "PhaseB" => _palettePhase.Z,
                        _ => double.NaN
                    };

                    if (double.IsFinite(value))
                    {
                        slider.Value = value;
                        UpdateParameterText(slider, parameter, value);
                    }
                }
            }
            finally
            {
                _isSynchronizingColorControls = false;
            }
        }

        private static IEnumerable<Slider> FindSliders(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is Slider slider)
                    yield return slider;

                foreach (var nestedSlider in FindSliders(child))
                    yield return nestedSlider;
            }
        }

        private void PresetsButton_Click(object sender, RoutedEventArgs e)
        {
            bool isOpening = PresetsPanel.Visibility == Visibility.Collapsed;
            PresetsPanel.Visibility = isOpening ? Visibility.Visible : Visibility.Collapsed;
            PresetsButton.Content = isOpening ? "Скрыть пресеты" : "Пресеты";
        }

        private void PresetSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || PresetSelector.SelectedIndex < 0 ||
                PresetSelector.SelectedIndex >= Presets.Length)
            {
                return;
            }

            var preset = Presets[PresetSelector.SelectedIndex];
            _power = preset.Power;
            _escapeRadius = preset.EscapeRadius;
            _maxIterations = preset.MaxIterations;
            _function = preset.Function;
            _coloringMethod = preset.ColoringMethod;
            _useSmooth = preset.UseSmooth;
            _orbitTrapType = preset.OrbitTrapType;
            _paletteBase = preset.Base;
            _paletteAmplitude = preset.Amplitude;
            _paletteFrequency = preset.Frequency;
            _palettePhase = preset.Phase;

            FunctionSelector.SelectedIndex = (int)_function;
            ColoringMethodSelector.SelectedIndex = (int)_coloringMethod;
            SmoothingMethodSelector.SelectedIndex = (int)_useSmooth;
            OrbitTrapTypeSelector.SelectedIndex = (int)_orbitTrapType;

            _isSynchronizingColorControls = true;
            try
            {
                foreach (var slider in FindSliders(LogicPanel))
                {
                    if (slider.Tag is not string parameter)
                        continue;

                    var value = parameter switch
                    {
                        "Power" => _power,
                        "EscapeRadius" => _escapeRadius,
                        "MaxIterations" => _maxIterations,
                        _ => double.NaN
                    };

                    if (double.IsFinite(value))
                    {
                        slider.Value = value;
                        UpdateParameterText(slider, parameter, value);
                    }
                }
            }
            finally
            {
                _isSynchronizingColorControls = false;
            }

            SetJuliaThumbPosition(
                10 + (preset.X + 1) * 110,
                10 + (1 - (preset.Y + 1) / 2) * 220);
            UpdatePaletteSliders();
            UpdateHsvFromPaletteBase();
            ApplyParameters();
        }

        private void ApplyParameters()
        {
            UpdateFractalInfo();
            _renderer?.SetParameters(
                new Vector2(_juliaX, _juliaY),
                _maxIterations,
                _escapeRadius,
                _power,
                _function,
                _coloringMethod,
                _useSmooth,
                _orbitTrapType,
                _paletteBase,
                _paletteAmplitude,
                _paletteFrequency,
                _palettePhase);
        }

        private void UpdateFractalInfo()
        {
            FractalNameText.Text = _function switch
            {
                0 => "Mandelbrot",
                1 => "Julia",
                2 => "Синус Mandelbrot",
                3 => "Синус Julia",
                4 => "Косинус Mandelbrot",
                5 => "Косинус Julia",
                _ => "Неизвестный фрактал"
            };

            FormulaText.Text = _function switch
            {
                2 or 3 => "zₙ₊₁ = sin(zₙ) + c",
                4 or 5 => "zₙ₊₁ = cos(zₙ) + c",
                _ => $"zₙ₊₁ = zₙ^{_power} + c"
            };

            ColoringMethodText.Text = _coloringMethod switch
            {
                0 => "Cosine palette",
                1 => "Monochrome",
                2 => "Orbit trap",
                3 => "Metallic surface",
                _ => "Неизвестный метод"
            };

            ScaleText.Text = $"Масштаб: {(_renderer?.ViewScale ?? 4.0):0.0000000}";
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            bool isOpening = ColorPanel.Visibility == Visibility.Collapsed;
            if (isOpening)
            {
                LogicPanel.Visibility = Visibility.Collapsed;
                ParametersButton.Content = "Параметры";
            }

            ColorPanel.Visibility = isOpening ? Visibility.Visible : Visibility.Collapsed;
            ColorButton.Content = isOpening ? "Скрыть палитру" : "Палитра";
        }

        private async void SavePngButton_Click(object sender, RoutedEventArgs e)
        {
            if (_renderer is null || sender is not Button button)
                return;

            var picker = new FileSavePicker
            {
                SuggestedFileName = "Fractal.png"
            };
            picker.FileTypeChoices.Add("PNG изображение", new List<string> { ".png" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return;

            try
            {
                button.IsEnabled = false;
                await _renderer.SavePngAsync(file);
            }
            catch (Exception exception)
            {
                var dialog = new ContentDialog
                {
                    Title = "Не удалось сохранить PNG",
                    Content = exception.Message,
                    CloseButtonText = "Закрыть",
                    XamlRoot = RootGrid.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            bool isOpening = SettingsStrip.Visibility == Visibility.Collapsed;
            SettingsStrip.Visibility = isOpening ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            bool isOpening = FractalInfoPanel.Visibility == Visibility.Collapsed;
            FractalInfoPanel.Visibility = isOpening ? Visibility.Visible : Visibility.Collapsed;
            InfoButton.Content = isOpening ? "Скрыть сводку" : "Сводка";
        }

        private void SettingsPanelCloseButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsStrip.Visibility = Visibility.Collapsed;
        }

        private void FractalInfoPanelCloseButton_Click(object sender, RoutedEventArgs e)
        {
            FractalInfoPanel.Visibility = Visibility.Collapsed;
            InfoButton.Content = "Сводка";
        }

        private void ParametersButton_Click(object sender, RoutedEventArgs e)
        {
            bool isOpening = LogicPanel.Visibility == Visibility.Collapsed;
            if (isOpening)
            {
                ColorPanel.Visibility = Visibility.Collapsed;
                ColorButton.Content = "Цвет";
            }

            LogicPanel.Visibility = isOpening ? Visibility.Visible : Visibility.Collapsed;
            ParametersButton.Content = isOpening ? "Скрыть параметры" : "Параметры";
        }

        private void ParametersPanelCloseButton_Click(object sender, RoutedEventArgs e)
        {
            LogicPanel.Visibility = Visibility.Collapsed;
            ParametersButton.Content = "Параметры";
        }

        private void ColorPanelCloseButton_Click(object sender, RoutedEventArgs e)
        {
            ColorPanel.Visibility = Visibility.Collapsed;
            ColorButton.Content = "Палитра";
        }

        private void FullScreenKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (_appWindow is not null)
            {
                bool isFullScreen = _appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
                _appWindow.SetPresenter(isFullScreen
                    ? AppWindowPresenterKind.Overlapped
                    : AppWindowPresenterKind.FullScreen);
            }

            args.Handled = true;
        }

        private void OpenParametersKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SettingsStrip.Visibility = SettingsStrip.Visibility == Visibility.Collapsed
                ? Visibility.Visible
                : Visibility.Collapsed;
            args.Handled = true;
        }

        private void ParametersButton_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {

        }
    }
}
