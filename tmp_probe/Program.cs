using Brackets.Core.Generation;
using Brackets.Core.Models;
using Brackets.Pdf;
using Brackets.Pdf.Diagram;

// 1) Long team names -> does the fixed 154x46 game box / leaf overflow and throw?
{
    var names = new Dictionary<int, string>();
    for (int i = 1; i <= 16; i++)
        names[i] = "The Exceptionally Long Team Name Number " + i + " From Springfield United FC";
    var bracket = BracketGenerator.Generate(new BracketOptions { TeamCount = 16, TeamNames = names });
    try
    {
        byte[] pdf = BracketDiagramRenderer.Render(bracket);
        Console.WriteLine($"Long names: OK ({pdf.Length} bytes)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Long names: THROW -> {ex.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine($"BoxH = {DiagramLayout.BoxH}");

try
{
    var bracket = BracketGenerator.Generate(new BracketOptions { TeamCount = 16 });
    var imgs = BracketDiagramRenderer.RenderImages(bracket, 96);
    Console.WriteLine($"RenderImages: OK ({imgs.Count} images, first={imgs[0].Length} bytes)");
}
catch (Exception ex)
{
    Console.WriteLine($"RenderImages: THROW -> {ex.GetType().Name}: {ex.Message}");
}
