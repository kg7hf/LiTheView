// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using LitheView.Designer.Core.Preview;
using LitheView.Designer.Core.Projects;
using LitheView.Designer.Core.Documents;
using Core = LitheView.Designer.Core;

namespace LitheView.Designer.App;

internal sealed class PreviewCanvas : Control
{
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(10);

    private readonly string _workerPath;
    private PreviewWorkerClient? _client;
    private Bitmap? _frame;
    private DesignerWorkspace? _workspace;
    private string _status = "Native preview has not rendered yet.";
    private int _zoomPercent = 100;
    private readonly HashSet<string> _selectedComponentIds = new(StringComparer.Ordinal);
    private string? _selectedComponentId;
    private string? _dragComponentId;
    private Point _dragStart;
    private readonly Dictionary<string, FormRectangle> _dragOriginalFrames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FormRectangle> _dragCurrentFrames = new(StringComparer.Ordinal);
    private bool _resizeDrag;
    private TextBox? _textEditor;
    private string? _textEditorComponentId;
    private bool _cancelTextEdit;

    public PreviewCanvas(string workerPath)
    {
        _workerPath = workerPath;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(37, 40, 45);
        ForeColor = Color.Gainsboro;
        Dock = DockStyle.Fill;
        TabStop = true;
        AccessibleName = "Native LitheView display preview";
        AccessibleDescription = "Pixels are rendered by the isolated native LitheView preview worker.";
    }

    public event EventHandler<string>? RenderStatusChanged;

    public event EventHandler<ComponentSelectionEventArgs>? ComponentSelectionRequested;

    public event EventHandler<ComponentFramesEditEventArgs>? ComponentFramesEditRequested;

    public event EventHandler<ComponentTextEditEventArgs>? ComponentTextEditRequested;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ZoomPercent
    {
        get => _zoomPercent;
        set
        {
            _zoomPercent = Math.Clamp(value, 25, 400);
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedComponentId
    {
        get => _selectedComponentId;
        set
        {
            _selectedComponentId = value;
            _selectedComponentIds.Clear();
            if (value is not null)
            {
                _selectedComponentIds.Add(value);
            }
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyCollection<string> SelectedComponentIds => _selectedComponentIds;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowGrid { get; set; } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SnapToGrid { get; set; } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int GridSize { get; set; } = 8;

    public void SetSelection(IEnumerable<string> componentIds, string? primaryComponentId)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        _selectedComponentIds.Clear();
        _selectedComponentIds.UnionWith(componentIds);
        _selectedComponentId = primaryComponentId;
        Invalidate();
    }

    public bool BeginTextEditSelected()
    {
        if (_workspace is null || _frame is null || _selectedComponentId is null)
        {
            return false;
        }
        FormComponent component = _workspace.Form.Components.Single(
            item => item.ComponentId == _selectedComponentId);
        FormTextValue? text = component.Properties.OfType<FormTextValue>()
            .SingleOrDefault(item => item.PropertyId == "text");
        if (text is null)
        {
            return false;
        }
        Rectangle destination = CalculateDestination(_frame.Size, ClientRectangle, _zoomPercent);
        BeginTextEdit(component, text.Value, destination);
        return true;
    }

    public async Task RenderAsync(DesignerWorkspace workspace, CancellationToken cancellationToken = default)
    {
        _workspace = workspace;
        if (!File.Exists(_workerPath))
        {
            SetStatus("Native preview worker is not built. Run the prepare Item 5 Native Preview task.");
            return;
        }

        Core.Documents.FormXmlWriteResult snapshot = Core.Documents.FormXmlWriter.Write(
            workspace.Form,
            workspace.Catalog);
        if (!snapshot.IsValid || snapshot.Utf8 is null)
        {
            SetStatus(string.Join(
                Environment.NewLine,
                snapshot.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
            return;
        }

        try
        {
            _client ??= new PreviewWorkerClient(_workerPath);
            PreviewSurface surface = PreviewSurfaceFactory.Create(workspace.ResolvedProject.Display);
            PreviewFrame rendered = await _client.RenderAsync(
                new PreviewRenderRequest(surface, snapshot.Utf8, RenderMode: PreviewRenderMode.FullFrame),
                RenderTimeout,
                cancellationToken);
            if (rendered.Status != PreviewStatus.Ok)
            {
                SetStatus($"Native preview {rendered.DiagnosticCode}: {rendered.Diagnostic}");
                return;
            }
            Bitmap replacement = ConvertFrame(rendered);
            Bitmap? old = _frame;
            _frame = replacement;
            old?.Dispose();
            SetStatus(
                $"Native preview: {surface.Width} x {surface.Height} {surface.PixelFormat}, " +
                $"{rendered.NativeRenderTime.TotalMilliseconds:F2} ms");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
                PreviewWorkerException or PlatformNotSupportedException)
        {
            SetStatus(exception.Message);
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.Clear(BackColor);
        eventArgs.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        if (_frame is null || _workspace is null)
        {
            TextRenderer.DrawText(
                eventArgs.Graphics,
                _status,
                Font,
                ClientRectangle,
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak);
            return;
        }

        Rectangle destination = CalculateDestination(_frame.Size, ClientRectangle, _zoomPercent);
        using SolidBrush shadow = new(Color.FromArgb(110, 0, 0, 0));
        eventArgs.Graphics.FillRectangle(
            shadow,
            new Rectangle(destination.X + 8, destination.Y + 8, destination.Width, destination.Height));
        eventArgs.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        eventArgs.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        eventArgs.Graphics.DrawImage(_frame, destination);
        DrawSurfaceAdorners(eventArgs.Graphics, destination);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs eventArgs)
    {
        base.OnMouseDoubleClick(eventArgs);
        if (eventArgs.Button != MouseButtons.Left || _workspace is null || _frame is null)
        {
            return;
        }
        Rectangle destination = CalculateDestination(_frame.Size, ClientRectangle, _zoomPercent);
        if (!destination.Contains(eventArgs.Location))
        {
            return;
        }
        int x = (eventArgs.X - destination.X) * _workspace.ResolvedProject.Display.Width /
            Math.Max(1, destination.Width);
        int y = (eventArgs.Y - destination.Y) * _workspace.ResolvedProject.Display.Height /
            Math.Max(1, destination.Height);
        FormComponent? hit = VisualOrder()
            .Reverse()
            .FirstOrDefault(component => Contains(AbsoluteFrame(component), x, y));
        FormTextValue? text = hit?.Properties.OfType<FormTextValue>()
            .SingleOrDefault(item => item.PropertyId == "text");
        if (hit is not null && text is not null)
        {
            BeginTextEdit(hit, text.Value, destination);
        }
    }

    protected override void OnMouseDown(MouseEventArgs eventArgs)
    {
        base.OnMouseDown(eventArgs);
        Focus();
        if (_frame is null || _workspace is null)
        {
            return;
        }
        Rectangle destination = CalculateDestination(_frame.Size, ClientRectangle, _zoomPercent);
        if (!destination.Contains(eventArgs.Location))
        {
            ComponentSelectionRequested?.Invoke(
                this,
                new ComponentSelectionEventArgs(componentId: null, toggle: false));
            return;
        }
        int x = (eventArgs.X - destination.X) * _workspace.ResolvedProject.Display.Width /
            Math.Max(1, destination.Width);
        int y = (eventArgs.Y - destination.Y) * _workspace.ResolvedProject.Display.Height /
            Math.Max(1, destination.Height);
        FormComponent? hit = VisualOrder()
            .Reverse()
            .FirstOrDefault(component => Contains(AbsoluteFrame(component), x, y));
        if (hit is not null)
        {
            bool toggle = (ModifierKeys & (Keys.Control | Keys.Shift)) != Keys.None;
            ComponentSelectionRequested?.Invoke(
                this,
                new ComponentSelectionEventArgs(hit.ComponentId, toggle));
            if (hit.ParentComponentId is not null && _selectedComponentIds.Contains(hit.ComponentId))
            {
                _dragComponentId = hit.ComponentId;
                _dragStart = eventArgs.Location;
                _dragOriginalFrames.Clear();
                _dragCurrentFrames.Clear();
                foreach (FormComponent selected in SelectedTopLevelComponents())
                {
                    FormRectangle frame = LocalFrame(selected);
                    _dragOriginalFrames.Add(selected.ComponentId, frame);
                    _dragCurrentFrames.Add(selected.ComponentId, frame);
                }
                FormRectangle absolute = AbsoluteFrame(hit);
                Point bottomRight = new(
                    destination.Left + (int)Math.Round((absolute.X + absolute.Width) *
                        (float)destination.Width / _workspace.ResolvedProject.Display.Width),
                    destination.Top + (int)Math.Round((absolute.Y + absolute.Height) *
                        (float)destination.Height / _workspace.ResolvedProject.Display.Height));
                _resizeDrag = _dragOriginalFrames.Count == 1 &&
                    Math.Abs(eventArgs.X - bottomRight.X) <= 10 &&
                    Math.Abs(eventArgs.Y - bottomRight.Y) <= 10;
                Capture = true;
            }
        }
        else
        {
            ComponentSelectionRequested?.Invoke(
                this,
                new ComponentSelectionEventArgs(componentId: null, toggle: false));
        }
    }

    protected override void OnMouseMove(MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        if (_dragComponentId is null || _workspace is null || _frame is null || !Capture)
        {
            return;
        }
        Rectangle destination = CalculateDestination(_frame.Size, ClientRectangle, _zoomPercent);
        int deltaX = (int)Math.Round((eventArgs.X - _dragStart.X) *
            (double)_workspace.ResolvedProject.Display.Width / Math.Max(1, destination.Width));
        int deltaY = (int)Math.Round((eventArgs.Y - _dragStart.Y) *
            (double)_workspace.ResolvedProject.Display.Height / Math.Max(1, destination.Height));
        FormRectangle primary = _dragOriginalFrames[_dragComponentId];
        if (SnapToGrid && GridSize > 1)
        {
            if (_resizeDrag)
            {
                deltaX = Snap(primary.Width + deltaX) - primary.Width;
                deltaY = Snap(primary.Height + deltaY) - primary.Height;
            }
            else
            {
                deltaX = Snap(primary.X + deltaX) - primary.X;
                deltaY = Snap(primary.Y + deltaY) - primary.Y;
            }
        }
        foreach ((string componentId, FormRectangle original) in _dragOriginalFrames)
        {
            _dragCurrentFrames[componentId] = _resizeDrag
                ? original with
                {
                    Width = Math.Max(1, original.Width + deltaX),
                    Height = Math.Max(1, original.Height + deltaY)
                }
                : original with { X = original.X + deltaX, Y = original.Y + deltaY };
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs eventArgs)
    {
        base.OnMouseUp(eventArgs);
        Capture = false;
        if (_dragComponentId is not null &&
            _dragCurrentFrames.Any(item => _dragOriginalFrames[item.Key] != item.Value))
        {
            ComponentFramesEditRequested?.Invoke(
                this,
                new ComponentFramesEditEventArgs(new Dictionary<string, FormRectangle>(
                    _dragCurrentFrames,
                    StringComparer.Ordinal)));
        }
        _dragComponentId = null;
        _dragOriginalFrames.Clear();
        _dragCurrentFrames.Clear();
        _resizeDrag = false;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _frame?.Dispose();
            _frame = null;
            _client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _client = null;
        }
        base.Dispose(disposing);
    }

    private void DrawSurfaceAdorners(Graphics graphics, Rectangle destination)
    {
        Core.Projects.DesignerDisplay display = _workspace!.ResolvedProject.Display;
        if (ShowGrid && GridSize > 1)
        {
            DrawGrid(graphics, destination, display.Width, display.Height);
        }
        Core.Profiles.SafeAreaProfile safe = display.SafeArea;
        float scaleX = (float)destination.Width / display.Width;
        float scaleY = (float)destination.Height / display.Height;
        Rectangle safeArea = Rectangle.FromLTRB(
            destination.Left + (int)Math.Round(safe.X * scaleX),
            destination.Top + (int)Math.Round(safe.Y * scaleY),
            destination.Left + (int)Math.Round((safe.X + safe.Width) * scaleX),
            destination.Top + (int)Math.Round((safe.Y + safe.Height) * scaleY));
        using Pen safePen = new(Color.FromArgb(200, Color.Gold), 1F)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        graphics.DrawRectangle(safePen, safeArea);
        using Pen edgePen = new(Color.FromArgb(200, Color.DeepSkyBlue), 1F);
        if (display.Shape == Core.Profiles.SurfaceShape.Circle)
        {
            graphics.DrawEllipse(edgePen, destination);
        }
        else
        {
            graphics.DrawRectangle(edgePen, destination);
        }
        foreach (string componentId in _selectedComponentIds)
        {
            FormComponent? selected = _workspace.Form.Components.SingleOrDefault(
                item => item.ComponentId == componentId);
            if (selected is not null)
            {
                FormRectangle frame = _dragCurrentFrames.TryGetValue(
                    selected.ComponentId,
                    out FormRectangle dragFrame)
                    ? AbsoluteFrame(selected, dragFrame)
                    : AbsoluteFrame(selected);
                Rectangle selection = Rectangle.FromLTRB(
                    destination.Left + (int)Math.Round(frame.X * scaleX),
                    destination.Top + (int)Math.Round(frame.Y * scaleY),
                    destination.Left + (int)Math.Round((frame.X + frame.Width) * scaleX),
                    destination.Top + (int)Math.Round((frame.Y + frame.Height) * scaleY));
                bool primary = selected.ComponentId == _selectedComponentId;
                using Pen selectionPen = new(
                    primary ? Color.DeepSkyBlue : Color.MediumPurple,
                    primary ? 2F : 1F);
                graphics.DrawRectangle(selectionPen, selection);
                if (primary)
                {
                    DrawSelectionHandles(graphics, selection);
                }
            }
        }
    }

    private void DrawGrid(
        Graphics graphics,
        Rectangle destination,
        int surfaceWidth,
        int surfaceHeight)
    {
        float scaleX = (float)destination.Width / surfaceWidth;
        float scaleY = (float)destination.Height / surfaceHeight;
        if (GridSize * Math.Min(scaleX, scaleY) < 4F)
        {
            return;
        }
        using Pen gridPen = new(Color.FromArgb(72, Color.White), 1F)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dot
        };
        for (int x = GridSize; x < surfaceWidth; x += GridSize)
        {
            int screenX = destination.Left + (int)Math.Round(x * scaleX);
            graphics.DrawLine(gridPen, screenX, destination.Top, screenX, destination.Bottom);
        }
        for (int y = GridSize; y < surfaceHeight; y += GridSize)
        {
            int screenY = destination.Top + (int)Math.Round(y * scaleY);
            graphics.DrawLine(gridPen, destination.Left, screenY, destination.Right, screenY);
        }
    }

    private void BeginTextEdit(FormComponent component, string text, Rectangle destination)
    {
        FinishTextEdit(commit: false);
        FormRectangle frame = AbsoluteFrame(component);
        float scaleX = (float)destination.Width /
            _workspace!.ResolvedProject.Display.Width;
        float scaleY = (float)destination.Height /
            _workspace.ResolvedProject.Display.Height;
        Rectangle bounds = Rectangle.FromLTRB(
            destination.Left + (int)Math.Round(frame.X * scaleX),
            destination.Top + (int)Math.Round(frame.Y * scaleY),
            destination.Left + (int)Math.Round((frame.X + frame.Width) * scaleX),
            destination.Top + (int)Math.Round((frame.Y + frame.Height) * scaleY));
        bounds.Intersect(ClientRectangle);
        _textEditorComponentId = component.ComponentId;
        _cancelTextEdit = false;
        _textEditor = new TextBox
        {
            Bounds = new Rectangle(
                bounds.X,
                bounds.Y,
                Math.Max(60, bounds.Width),
                Math.Max(Font.Height + 8, bounds.Height)),
            Text = text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Font,
            AccessibleName = $"Edit text for {component.Name}"
        };
        _textEditor.KeyDown += TextEditorKeyDown;
        _textEditor.LostFocus += TextEditorLostFocus;
        Controls.Add(_textEditor);
        _textEditor.BringToFront();
        _textEditor.SelectAll();
        _textEditor.Focus();
    }

    private void TextEditorKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.KeyCode == Keys.Enter)
        {
            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
            FinishTextEdit(commit: true);
        }
        else if (eventArgs.KeyCode == Keys.Escape)
        {
            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
            _cancelTextEdit = true;
            FinishTextEdit(commit: false);
        }
    }

    private void TextEditorLostFocus(object? sender, EventArgs eventArgs)
    {
        if (_textEditor is not null)
        {
            FinishTextEdit(commit: !_cancelTextEdit);
        }
    }

    private void FinishTextEdit(bool commit)
    {
        TextBox? editor = _textEditor;
        string? componentId = _textEditorComponentId;
        _textEditor = null;
        _textEditorComponentId = null;
        if (editor is null)
        {
            return;
        }
        string text = editor.Text;
        editor.KeyDown -= TextEditorKeyDown;
        editor.LostFocus -= TextEditorLostFocus;
        Controls.Remove(editor);
        editor.Dispose();
        Focus();
        if (commit && componentId is not null)
        {
            ComponentTextEditRequested?.Invoke(this, new ComponentTextEditEventArgs(componentId, text));
        }
    }

    private int Snap(int value)
    {
        return checked((int)Math.Round(
            value / (double)GridSize,
            MidpointRounding.AwayFromZero) * GridSize);
    }

    private IEnumerable<FormComponent> VisualOrder()
    {
        foreach (FormComponent root in _workspace!.Form.Components
                     .Where(item => item.ParentComponentId is null)
                     .OrderBy(item => item.SiblingOrder))
        {
            foreach (FormComponent component in VisualOrder(root))
            {
                yield return component;
            }
        }
    }

    private IEnumerable<FormComponent> VisualOrder(FormComponent parent)
    {
        yield return parent;
        foreach (FormComponent child in _workspace!.Form.Components
                     .Where(item => item.ParentComponentId == parent.ComponentId)
                     .OrderBy(item => item.SiblingOrder))
        {
            foreach (FormComponent descendant in VisualOrder(child))
            {
                yield return descendant;
            }
        }
    }

    private IEnumerable<FormComponent> SelectedTopLevelComponents()
    {
        foreach (FormComponent component in _workspace!.Form.Components.Where(item =>
                     item.ParentComponentId is not null &&
                     _selectedComponentIds.Contains(item.ComponentId)))
        {
            string? parentId = component.ParentComponentId;
            bool selectedAncestor = false;
            while (parentId is not null)
            {
                if (_selectedComponentIds.Contains(parentId))
                {
                    selectedAncestor = true;
                    break;
                }
                parentId = _workspace.Form.Components
                    .Single(item => item.ComponentId == parentId)
                    .ParentComponentId;
            }
            if (!selectedAncestor)
            {
                yield return component;
            }
        }
    }

    private FormRectangle AbsoluteFrame(FormComponent component)
    {
        return AbsoluteFrame(component, LocalFrame(component));
    }

    private FormRectangle AbsoluteFrame(FormComponent component, FormRectangle localFrame)
    {
        FormRectangle frame = localFrame;
        string? parentId = component.ParentComponentId;
        while (parentId is not null)
        {
            FormComponent parent = _workspace!.Form.Components.Single(item => item.ComponentId == parentId);
            FormRectangle parentFrame = parent.Properties
                .OfType<FormRectangleValue>()
                .Single(item => item.PropertyId == "frame").Value;
            frame = frame with { X = frame.X + parentFrame.X, Y = frame.Y + parentFrame.Y };
            parentId = parent.ParentComponentId;
        }
        return frame;
    }

    private static FormRectangle LocalFrame(FormComponent component)
    {
        return component.Properties
            .OfType<FormRectangleValue>()
            .Single(item => item.PropertyId == "frame").Value;
    }

    private static void DrawSelectionHandles(Graphics graphics, Rectangle selection)
    {
        const int handleSize = 7;
        Point[] points =
        [
            new(selection.Left, selection.Top),
            new(selection.Right, selection.Top),
            new(selection.Left, selection.Bottom),
            new(selection.Right, selection.Bottom)
        ];
        using SolidBrush handle = new(Color.White);
        using Pen outline = new(Color.DeepSkyBlue);
        foreach (Point point in points)
        {
            Rectangle bounds = new(
                point.X - (handleSize / 2),
                point.Y - (handleSize / 2),
                handleSize,
                handleSize);
            graphics.FillRectangle(handle, bounds);
            graphics.DrawRectangle(outline, bounds);
        }
    }

    private static bool Contains(FormRectangle rectangle, int x, int y)
    {
        return x >= rectangle.X && y >= rectangle.Y &&
            x < rectangle.X + rectangle.Width && y < rectangle.Y + rectangle.Height;
    }

    private void SetStatus(string status)
    {
        _status = status;
        RenderStatusChanged?.Invoke(this, status);
        Invalidate();
    }

    private static Rectangle CalculateDestination(Size frame, Rectangle client, int zoomPercent)
    {
        int availableWidth = Math.Max(1, client.Width - 48);
        int availableHeight = Math.Max(1, client.Height - 48);
        float fit = Math.Min((float)availableWidth / frame.Width, (float)availableHeight / frame.Height);
        float scale = fit * zoomPercent / 100F;
        int width = Math.Max(1, (int)Math.Round(frame.Width * scale));
        int height = Math.Max(1, (int)Math.Round(frame.Height * scale));
        return new Rectangle(
            client.Left + (client.Width - width) / 2,
            client.Top + (client.Height - height) / 2,
            width,
            height);
    }

    // Presentation only: Core decodes completed native storage, including packed emitters,
    // and this copies its RGBA rows into the 32-bit BGRA bitmap the canvas draws.
    private static Bitmap ConvertFrame(PreviewFrame frame)
    {
        PreviewImage image = PreviewPresentation.ToRgba(frame);
        Bitmap bitmap = new(image.Width, image.Height, PixelFormat.Format32bppArgb);
        Rectangle bounds = new(0, 0, bitmap.Width, bitmap.Height);
        BitmapData locked = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] row = new byte[checked(image.Width * 4)];
            for (int y = 0; y < image.Height; ++y)
            {
                image.Rgba.AsSpan(y * row.Length, row.Length).CopyTo(row);
                for (int x = 0; x < row.Length; x += 4)
                {
                    (row[x], row[x + 2]) = (row[x + 2], row[x]);
                }
                Marshal.Copy(row, 0, IntPtr.Add(locked.Scan0, y * locked.Stride), row.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(locked);
        }
        return bitmap;
    }
}

internal sealed class ComponentSelectionEventArgs(string? componentId, bool toggle) : EventArgs
{
    public string? ComponentId { get; } = componentId;

    public bool Toggle { get; } = toggle;
}

internal sealed class ComponentFramesEditEventArgs(
    IReadOnlyDictionary<string, FormRectangle> frames) : EventArgs
{
    public IReadOnlyDictionary<string, FormRectangle> Frames { get; } = frames;
}

internal sealed class ComponentTextEditEventArgs(string componentId, string text) : EventArgs
{
    public string ComponentId { get; } = componentId;

    public string Text { get; } = text;
}
