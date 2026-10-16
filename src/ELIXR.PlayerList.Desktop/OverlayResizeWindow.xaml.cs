using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ELIXR.PlayerList.Core.Models;

namespace ELIXR.PlayerList.Desktop;

public partial class OverlayResizeWindow : Window
{
    private readonly DesktopOverlayWindow _target;
    private readonly DesktopOverlayBounds _original;
    private readonly Func<DesktopOverlayBounds, Task<bool>> _save;
    private readonly List<Thumb> _handles = [];
    private readonly DesktopOverlayBounds _screen;
    private bool _updating = true, _saved, _closing, _saving, _disposed, _forceClose;
    internal bool WasSaved => _saved;
    public OverlayResizeWindow(DesktopOverlayWindow target, Func<DesktopOverlayBounds, Task<bool>> save)
    {
        InitializeComponent();
        _target = target; _save = save; _original = target.Placement;
        _screen = new(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        Left = _screen.Left; Top = _screen.Top; Width = _screen.Width; Height = _screen.Height;
        Owner = target;
        WidthSlider.Maximum = _screen.Width; HeightSlider.Maximum = _screen.Height;
        foreach (var edge in new[] { "nw", "n", "ne", "e", "se", "s", "sw", "w", "move" })
        {
            var thumb = new Thumb { Tag = edge, Width = edge == "move" ? 112 : 16, Height = edge == "move" ? 26 : 16,
                Cursor = edge == "move" ? Cursors.SizeAll : edge is "n" or "s" ? Cursors.SizeNS : edge is "e" or "w" ? Cursors.SizeWE : edge is "nw" or "se" ? Cursors.SizeNWSE : Cursors.SizeNESW };
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(edge == "move" ? 8 : 5));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(239, 133, 183)));
            border.SetValue(Border.BorderBrushProperty, Brushes.White); border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            if (edge == "move")
            {
                var label = new FrameworkElementFactory(typeof(TextBlock)); label.SetValue(TextBlock.TextProperty, "✥  Drag to move"); label.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(38, 20, 37))); label.SetValue(TextBlock.FontSizeProperty, 11d); label.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center); label.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(label);
            }
            thumb.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = border };
            thumb.DragDelta += HandleDrag;
            Handles.Children.Add(thumb); _handles.Add(thumb);
        }
        Loaded += (_, _) =>
        {
            _updating = false; Refresh(); Panel.UpdateLayout(); PositionPanel();
            if (SystemParameters.ClientAreaAnimation)
            {
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                PanelSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(360)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .4 } });
                foreach (var handle in _handles)
                {
                    var scale = new ScaleTransform(1,1); handle.RenderTransform = scale;handle.RenderTransformOrigin = new Point(.5,.5);
                    var pop = new DoubleAnimation(.65,1,TimeSpan.FromMilliseconds(280)) { EasingFunction = new BackEase { EasingMode=EasingMode.EaseOut,Amplitude=.4 } };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty,pop);scale.BeginAnimation(ScaleTransform.ScaleYProperty,pop);
                }
            }
            SaveButton.Focus();
        };
        Closing += (_, e) => { if (_saving&&!_forceClose) {e.Cancel=true;return;} if (!_saved) _target.ApplyPlacement(_original); };
        Closed += (_, _) => _disposed = true;
    }
    private void HandleDrag(object sender, DragDeltaEventArgs e)
    {
        if (_saving || _closing) return;
        var edge = (string)((Thumb)sender).Tag;
        var b = _target.Placement;
        var left = b.Left; var top = b.Top; var width = b.Width; var height = b.Height;
        if (edge == "move") { left += e.HorizontalChange; top += e.VerticalChange; }
        else
        {
            if (edge.Contains('w')) { var delta = Math.Clamp(e.HorizontalChange,_screen.Left-left,width-240);left+=delta;width-=delta; }
            if (edge.Contains('e')) width = Math.Clamp(width+e.HorizontalChange,240,Math.Max(240,_screen.Left+_screen.Width-left));
            if (edge.Contains('n')) { var delta = Math.Clamp(e.VerticalChange,_screen.Top-top,height-240);top+=delta;height-=delta; }
            if (edge.Contains('s')) height = Math.Clamp(height+e.VerticalChange,240,Math.Max(240,_screen.Top+_screen.Height-top));
        }
        _target.ApplyPlacement(DesktopOverlayBounds.Clamp(new(left, top, width, height), _screen)); Refresh();
    }
    private void Refresh()
    {
        var b = _target.Placement;
        _updating = true; WidthSlider.Value = b.Width; HeightSlider.Value = b.Height; _updating = false;
        WidthLabel.Text = $"{b.Width:0} px"; HeightLabel.Text = $"{b.Height:0} px"; PositionLabel.Text = $"Position   {b.Left:0}, {b.Top:0}";
        var x = b.Left - Left; var y = b.Top - Top;
        var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
        geometry.Children.Add(new RectangleGeometry(new Rect(0, 0, Width, Height)));
        geometry.Children.Add(new RectangleGeometry(new Rect(x, y, b.Width, b.Height), 17, 17));
        Shade.Data = geometry;
        Canvas.SetLeft(PreviewShield,x);Canvas.SetTop(PreviewShield,y);PreviewShield.Width=b.Width;PreviewShield.Height=b.Height;
        Canvas.SetLeft(Frame, x - 2); Canvas.SetTop(Frame, y - 2); Frame.Width = b.Width + 4; Frame.Height = b.Height + 4;
        foreach (var thumb in _handles)
        {
            var edge = (string)thumb.Tag;
            var hx = edge.Contains('w') ? x : edge.Contains('e') ? x + b.Width : x + b.Width / 2;
            var hy = edge.Contains('n') ? y : edge.Contains('s') ? y + b.Height : y + b.Height / 2;
            if (edge == "move") { hx = x + b.Width / 2; hy = y + 18; }
            Canvas.SetLeft(thumb, Math.Clamp(hx - thumb.Width / 2, 0, Math.Max(0, Width - thumb.Width)));
            Canvas.SetTop(thumb, Math.Clamp(hy - thumb.Height / 2, 0, Math.Max(0, Height - thumb.Height)));
        }
        if (Panel.ActualHeight > 0 && double.IsFinite(Canvas.GetLeft(Panel)))
        {
            var controls = new Rect(Canvas.GetLeft(Panel),Canvas.GetTop(Panel),Panel.ActualWidth,Panel.ActualHeight);
            var preview = new Rect(x-10,y-10,b.Width+20,b.Height+20);
            if(controls.IntersectsWith(preview)) PositionPanel();
        }
    }
    internal void RefreshPlacement() { if (IsLoaded && !_disposed && !_closing) Refresh(); }
    private void PositionPanel()
    {
        var work = SystemParameters.WorkArea;
        var b = _target.Placement; var panelWidth = Panel.ActualWidth; var panelHeight = Panel.ActualHeight;
        var left = b.Left - panelWidth - 28;
        if (left < work.Left + 18) left = b.Left + b.Width + 28;
        left = Math.Clamp(left, work.Left + 12, Math.Max(work.Left + 12, work.Right - panelWidth - 12));
        var top = Math.Clamp(b.Top, work.Top + 12, Math.Max(work.Top + 12, work.Bottom - panelHeight - 12));
        var from = Canvas.GetLeft(Panel);var to = left - Left;
        Canvas.SetLeft(Panel, to); Canvas.SetTop(Panel, top - Top);
        if(SystemParameters.ClientAreaAnimation&&double.IsFinite(from)&&Math.Abs(from-to)>1)
            Panel.BeginAnimation(Canvas.LeftProperty,new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
    }
    private void SizeChangedBySlider(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || _saving || _closing) return;
        _target.ApplyPlacement(DesktopOverlayBounds.Clamp(_target.Placement with { Width = WidthSlider.Value, Height = HeightSlider.Value }, _screen)); Refresh();
    }
    private void ResetSize(object sender, RoutedEventArgs e) { if (_saving) return; _target.ApplyPlacement(DesktopOverlayBounds.Clamp(_target.Placement with { Width = 440, Height = 660 }, _screen)); Refresh(); }
    private async void SaveClick(object sender, RoutedEventArgs e) => await SaveAsync();
    internal async Task<bool> SaveAsync()
    {
        if (_saving || _closing) return false;
        _saving = true; SaveButton.IsEnabled = CancelButton.IsEnabled = false; SaveButton.Content="Saving…";ErrorLabel.Text = "";
        var success = await _save(_target.Placement);
        if (_disposed) return false;
        _saving = false; SaveButton.IsEnabled = CancelButton.IsEnabled = true;SaveButton.Content="Save layout";
        if (!success) { ErrorLabel.Text = "Could not save this layout. Please try again, or cancel.";Panel.UpdateLayout();PositionPanel();return false; }
        _saved = true; Finish(); return true;
    }
    private void CancelClick(object sender, RoutedEventArgs e) => CancelAndClose();
    internal void CancelAndClose(bool immediate = false) { if (_saving && !immediate) return; if (_disposed) return; _target.ApplyPlacement(_original); if (immediate) {_forceClose=true;Close();} else Finish(); }
    private void Finish()
    {
        if (_closing) return; _closing = true;
        if (!SystemParameters.ClientAreaAnimation) { Close(); return; }
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140)); fade.Completed += (_, _) => Close(); BeginAnimation(OpacityProperty, fade);
    }
    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; CancelAndClose(); }
        else if (e.Key == Key.Enter) { e.Handled = true; await SaveAsync(); }
    }
#if DEBUG
    internal void PreviewSize(double width, double height) { WidthSlider.Value = width; HeightSlider.Value = height; }
#endif
}
