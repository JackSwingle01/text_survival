namespace text_survival.UI;

/// <summary>
/// 2D character grid for systematic map rendering.
/// Grid coordinates are [y, x] where y is row, x is column.
/// </summary>
public class CharacterGrid
{
    public readonly char[,] _grid;
    public int Width { get; }
    public int Height { get; }

    public CharacterGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _grid = new char[height, width];

        // Initialize with spaces
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _grid[y, x] = ' ';
            }
        }
    }

    /// <summary>
    /// Sets a single character at the specified position
    /// </summary>
    public void SetChar(int x, int y, char c)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            _grid[y, x] = c;
        }
    }

    /// <summary>
    /// Writes a string horizontally starting at the specified position
    /// </summary>
    public void SetText(int x, int y, string text)
    {
        if (y < 0 || y >= Height) return;

        for (int i = 0; i < text.Length && (x + i) < Width; i++)
        {
            if (x + i >= 0)
            {
                _grid[y, x + i] = text[i];
            }
        }
    }

    /// <summary>
    /// Draws a box with borders at the specified position
    /// </summary>
    public void DrawBox(int x, int y, int width, int height)
    {
        if (width < 2 || height < 2) return;

        // Top border
        SetChar(x, y, '┌');
        for (int i = 1; i < width - 1; i++)
        {
            SetChar(x + i, y, '─');
        }
        SetChar(x + width - 1, y, '┐');

        // Side borders
        for (int i = 1; i < height - 1; i++)
        {
            SetChar(x, y + i, '│');
            SetChar(x + width - 1, y + i, '│');
        }

        // Bottom border
        SetChar(x, y + height - 1, '└');
        for (int i = 1; i < width - 1; i++)
        {
            SetChar(x + i, y + height - 1, '─');
        }
        SetChar(x + width - 1, y + height - 1, '┘');
    }

    /// <summary>
    /// Converts the grid to a string for display
    /// </summary>
    public string GetRenderedString()
    {
        var sb = new System.Text.StringBuilder();

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                sb.Append(_grid[y, x]);
            }
            if (y < Height - 1) // Don't add newline after last row
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
