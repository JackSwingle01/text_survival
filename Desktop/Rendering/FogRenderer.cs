using System.Numerics;
using Raylib_cs;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Rendering;

/// <summary>Composites the map in tile-sized regions, including its connected ground details.</summary>
public sealed class FogRenderer : IDisposable
{
    private RenderTexture2D _scene;
    private Shader _grey;

    public void Begin()
    {
        int width = Raylib.GetScreenWidth(), height = Raylib.GetScreenHeight();
        if (_scene.Id == 0 || _scene.Texture.Width != width || _scene.Texture.Height != height)
        {
            if (_scene.Id != 0) Raylib.UnloadRenderTexture(_scene);
            _scene = Raylib.LoadRenderTexture(width, height);
        }
        if (_grey.Id == 0)
            _grey = Raylib.LoadShaderFromMemory(null, """
                #version 330
                in vec2 fragTexCoord;
                in vec4 fragColor;
                uniform sampler2D texture0;
                out vec4 finalColor;
                void main() {
                    vec4 color = texture(texture0, fragTexCoord) * fragColor;
                    float grey = dot(color.rgb, vec3(0.299, 0.587, 0.114));
                    finalColor = vec4(vec3(0.10 + grey * 0.45), color.a);
                }
                """);
        Raylib.BeginTextureMode(_scene);
        Raylib.ClearBackground(Color.Blank);
    }

    public void End(Camera camera, GameMap map)
    {
        Raylib.EndTextureMode();
        Raylib.BeginScissorMode(camera.ScreenOffsetX, camera.ScreenOffsetY, camera.GridWidth, camera.GridHeight);
        foreach (var (x, y) in camera.GetVisibleTiles())
        {
            var position = camera.WorldToScreen(x, y);
            // Include the seams so rivers crossing tile boundaries receive the same fog.
            int left = (int)MathF.Floor(position.X - camera.TileGap / 2f);
            int top = (int)MathF.Floor(position.Y - camera.TileGap / 2f);
            int right = (int)MathF.Floor(position.X + camera.TileSize + camera.TileGap / 2f);
            int bottom = (int)MathF.Floor(position.Y + camera.TileSize + camera.TileGap / 2f);
            var destination = new Rectangle(left, top, right - left, bottom - top);
            var visibility = map.GetVisibility(x, y);
            if (visibility == TileVisibility.Unexplored)
            {
                Raylib.DrawRectangleRec(destination, new Color(8, 10, 12, 255));
                continue;
            }

            if (visibility == TileVisibility.Explored) Raylib.BeginShaderMode(_grey);
            // Render textures have an inverted Y axis.
            var source = new Rectangle(left, _scene.Texture.Height - bottom, right - left, -(bottom - top));
            Raylib.DrawTexturePro(_scene.Texture, source, destination, Vector2.Zero, 0, Color.White);
            if (visibility == TileVisibility.Explored) Raylib.EndShaderMode();
        }
        Raylib.EndScissorMode();
    }

    public void Dispose()
    {
        if (_scene.Id != 0) Raylib.UnloadRenderTexture(_scene);
        if (_grey.Id != 0) Raylib.UnloadShader(_grey);
        _scene = default;
        _grey = default;
    }
}
