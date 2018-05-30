#r "System.Drawing"

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

var rootFolder = Path.GetFullPath(@"..\");
var sourceFolder = Path.Combine(rootFolder, "orig");
var targetFolder = Path.Combine(rootFolder, "misc");
var targetMaxSize = 48;
var plantUmlPath = @"plantuml.jar";
var inkScapePath = @"C:\Program Files (Portable)\inkscape\inkscape.exe";

Main();

public void Main()
{
    foreach (var imgPath in Directory.GetFiles(sourceFolder, "*.png", SearchOption.AllDirectories))
    {
        Console.WriteLine($"Processing {imgPath}");
        // Convert to png with white background
        var imgWithBg = ConvertToPng(imgPath, true);
        // Convert to puml
        var pumlPath = ConvertToPuml(imgWithBg);
        // Convert to pngs with transparent background
        ConvertToPng(imgPath, false);
    }
    GenerateMarkdownTable();
    Console.WriteLine("Finished");
}

public string ConvertToPng(string imgPath, bool withBackground)
{
    // Convert the image
    var backgroundOpacity = withBackground ? "1.0" : "0.0";
    var entityName = Path.GetFileNameWithoutExtension(imgPath);
    var targetFilePath = imgPath.Replace(@"\orig\", @"\misc\");
    var process = Process.Start(new ProcessStartInfo
    {
        FileName = inkScapePath,
        Arguments = $"-z --file=\"{imgPath}\" --export-png=\"{targetFilePath}\" --export-background=#FFFFFF --export-background-opacity={backgroundOpacity}",
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
    });
    if (!process.WaitForExit(20000))
    {
        Console.WriteLine("Killing");
        process.Kill();
    }
    // Scale the image
    Image newImage = null;
    using (var image = Image.FromFile(targetFilePath))
    {
        newImage = ScaleImage(image, targetMaxSize, targetMaxSize);
    }
    newImage.Save(targetFilePath, ImageFormat.Png);
    newImage.Dispose();
    return targetFilePath;
}

public string ConvertToPuml(string pngPath)
{
    var format = "16"; // 16z for compressed

    var entityName = Path.GetFileNameWithoutExtension(pngPath);
    var entityNameUpper = entityName.ToUpper();
    var pumlPath = Path.Combine(Directory.GetParent(pngPath).FullName, entityName + ".puml");
    var process = Process.Start(new ProcessStartInfo
    {
        WorkingDirectory = rootFolder,
        FileName = "java",
        Arguments = $"-jar {plantUmlPath} -encodesprite {format} \"{pngPath}\"",
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
    });
    process.WaitForExit();
    var sbImage = new StringBuilder();
    sbImage.Append(process.StandardOutput.ReadToEnd());
    sbImage.AppendLine($"!define MISC_{entityNameUpper}(_alias) ENTITY(rectangle,black,{entityName},_alias,MISC {entityNameUpper})");
    sbImage.AppendLine($"!define MISC_{entityNameUpper}(_alias,_label) ENTITY(rectangle,black,{entityName},_label,_alias,MISC {entityNameUpper})");
    sbImage.AppendLine($"!define MISC_{entityNameUpper}(_alias,_label,_shape) ENTITY(_shape,black,{entityName},_label,_alias,MISC {entityNameUpper})");
    sbImage.AppendLine($"!define MISC_{entityNameUpper}(_alias,_label,_shape,_color) ENTITY(_shape,_color,{entityName},_label,_alias,MISC {entityNameUpper})");

    File.WriteAllText(pumlPath, sbImage.ToString());
    return pumlPath;
}

public void GenerateMarkdownTable()
{
    // Create a markdown table with all entries
    var sbTable = new StringBuilder();
    sbTable.AppendLine("Macro | Image | Url");
    sbTable.AppendLine("--- | --- | ---");
    foreach (var filePath in Directory.GetFiles(targetFolder, "*.puml", SearchOption.AllDirectories))
    {
        var entityName = Path.GetFileNameWithoutExtension(filePath);
        sbTable.AppendLine($"MISC_{entityName.ToUpper()} | ![{entityName}](/misc/{entityName}.png?raw=true) | {entityName}.puml");
    }
    File.WriteAllText("../table.md", sbTable.ToString());
}

private static Image ScaleImage(Image image, int maxWidth, int maxHeight)
{
    var ratioX = (double)maxWidth / image.Width;
    var ratioY = (double)maxHeight / image.Height;
    var ratio = Math.Min(ratioX, ratioY);

    var newWidth = (int)(image.Width * ratio);
    var newHeight = (int)(image.Height * ratio);

    var newImage = new Bitmap(newWidth, newHeight);

    using (var graphics = Graphics.FromImage(newImage))
        graphics.DrawImage(image, 0, 0, newWidth, newHeight);

    return newImage;
}
