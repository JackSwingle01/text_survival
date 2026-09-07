using System.Text.Json.Serialization;

namespace text_survival.Desktop.UI;

public sealed record TextControl(string Id, string Kind, string Label, bool Enabled, object? Value)
{
    public List<string> Details { get; } = [];
}

/// <summary>A single immediate-mode screen observation. IDs are scoped to their screen
/// and controls are revalidated on the frame that consumes input.</summary>
public sealed class TextFrame(string? activate = null, string? input = null)
{
    private readonly List<string> _path = [];
    private readonly Stack<bool> _disabled = new();
    private readonly Dictionary<string, int> _occurrences = [];
    private TextControl? _lastControl;
    private string _lastText = "";
    private bool _join;
    [JsonIgnore] public string? Input { get; } = input;
    [JsonIgnore] public bool Activated { get; private set; }
    [JsonIgnore] public int TooltipDepth { get; set; }
    public List<string> Lines { get; } = [];
    public List<TextControl> Controls { get; } = [];

    public void Push(string id) => _path.Add(id);
    public void Pop() => _path.RemoveAt(_path.Count - 1);
    public void Disable(bool disabled) => _disabled.Push(disabled || (_disabled.TryPeek(out var parent) && parent));
    public void EndDisable() => _disabled.Pop();
    /// <summary>ImGui's SameLine: the next text belongs on the line already written.</summary>
    public void Join() => _join = true;
    public void Text(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        bool join = _join;
        _join = false;
        if (TooltipDepth > 0 && _lastControl != null)
            _lastControl.Details.Add(text);
        else if (join && Lines.Count > 0) Lines[^1] += " " + text;
        else Lines.Add(text);
        if (TooltipDepth == 0) { _lastText = text; _lastControl = null; }
    }
    public void DrawText(string text)
    {
        if (_lastControl != null)
        {
            if (_lastControl.Label.Length == 0)
            {
                var named = _lastControl with { Label = text };
                Controls[Controls.Count - 1] = named;
                _lastControl = named;
            }
            else _lastControl.Details.Add(text);
        }
        else Text(text);
    }
    public void Bar(float fraction, string overlay) => Text(string.IsNullOrEmpty(overlay) ? $"{fraction:P0}" : overlay);
    public bool ClickText() => Control(_lastText, "link");
    public bool Control(string label, string kind, object? value = null)
    {
        _join = false;
        var hash = label.IndexOf("###", StringComparison.Ordinal);
        int suffix = label.IndexOf("##", StringComparison.Ordinal);
        string identity = hash >= 0 ? label[(hash + 3)..] : suffix >= 0 ? label[(suffix + 2)..] : label;
        string key = string.Join('/', _path.Append(identity).Select(Uri.EscapeDataString));
        int occurrence = _occurrences.GetValueOrDefault(key);
        _occurrences[key] = occurrence + 1;
        string id = occurrence == 0 ? key : $"{key}~{occurrence}";
        int hidden = label.IndexOf("##", StringComparison.Ordinal);
        string display = hidden < 0 ? label : label[..hidden];
        bool enabled = !_disabled.TryPeek(out var disabled) || !disabled;
        var control = new TextControl(id, kind, display, enabled, value);
        Controls.Add(control);
        _lastControl = control;
        if (activate != id || Activated) return false;
        if (!enabled) throw new ArgumentException("That control is disabled.");
        Activated = true;
        return true;
    }
}
