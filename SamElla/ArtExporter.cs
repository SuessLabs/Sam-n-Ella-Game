namespace SamElla;

internal static class ArtExporter
{
  public static void Export(string assetsPath, string outputPath)
  {
    Directory.CreateDirectory(outputPath);
    var palette = Sprite.LoadPalette(Path.Combine(assetsPath, "palette.txt"));
    string[] files = Directory.GetFiles(assetsPath, "*.sprite").Order().ToArray();
    var sprites = files.Select(file => Sprite.Load(file, palette)).ToArray();
    int cellWidth = Math.Max(31, sprites.Max(sprite => sprite.Width) + 4);
    int cellHeight = Math.Max(50, sprites.Max(sprite => sprite.Height) + 14);
    int sheetRows = (files.Length + 7) / 8;

    var sheet = new PixelBuffer(8 + (8 * cellWidth), Math.Max(144, 29 + (sheetRows * cellHeight)));

    sheet.Clear(0xFF17263F);
    PixelFont.Draw(sheet, "Sam n' Ella - PIXEL ART", 8, 7, 0xFFFFD56B);

    for (int i = 0; i < files.Length; i++)
    {
      var sprite = sprites[i];
      var image = new PixelBuffer(sprite.Width, sprite.Height);
      image.Draw(sprite, 0, 0);
      image.SavePng(Path.Combine(outputPath, Path.GetFileNameWithoutExtension(files[i]) + ".png"));
      int x = 8 + ((i % 8) * cellWidth);
      int y = 29 + ((i / 8) * cellHeight);
      sheet.Fill(x - 1, y - 1, sprite.Width + 2, sprite.Height + 2, 0xFF355575);
      sheet.Draw(sprite, x, y);
      PixelFont.Draw(sheet, (i + 1).ToString(), x, y + Math.Max(21, sprite.Height + 3), 0xFFF6F4DF);
    }

    sheet.SavePng(Path.Combine(outputPath, "sprite-sheet.png"));
    var renderer = new PixelRenderer(assetsPath);
    var levels = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Levels"), "*.txt").Order().ToArray();
    var game = new Game(levels);

    for (int i = 0; i < 10; i++)
      game.Update(0, false);

    renderer.Draw(game, 0);
    renderer.Frame.SavePng(Path.Combine(outputPath, "game-preview.png"));
  }
}
