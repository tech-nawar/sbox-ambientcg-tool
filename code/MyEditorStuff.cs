using System;
using Editor;

public class feeffe
{
    //
    // An Editor menu will put the option in the main editor's menu
    //
    [Menu("Editor", "My Editor Stuff/Example Menu Option")]
    public static void ExampleMenuOption()
    {
        Log.Info("Menu option selected! Well done!");

        //https://ambientcg.com/api/v2/full_json?type=Material&include=downloadData
        //https://ambientcg.com/get?file=Asphalt027C_1K-JPG.zip

        DownloadTest();
    }

    async static void DownloadTest()
    {
        var api = new AmbientCG.AmbientCGAPI();
        var search = api.Search(
            new AmbientCG.SearchParameters() { Types = new[] { AmbientCG.AssetType.Material }, }
        );

        await foreach (var d in search)
        {
            Log.Info(d);
        }

        Log.Info("download test");
        var fileUrl = "https://ambientcg.com/get?file=Asphalt027C_1K-JPG.zip";
        var filePath = "mat.zip";
        var fileLocation = $"data/{filePath}";
        try
        {
            var success = await Utility.DownloadAsync(
                fileUrl,
                fileLocation,
                System.Threading.CancellationToken.None
            );
            Log.Info("finished");
        }
        catch (Exception e)
        {
            Log.Error("Error!");
            Log.Error(e);
            return;
        }

        var zipStream = FileSystem.Root.OpenRead(filePath, System.IO.FileMode.Open);

        var zip = Zip.ZipStorer.Open(zipStream, System.IO.FileAccess.Read);

        var dir = zip.ReadCentralDir();
        foreach (var file in dir)
        {
            Log.Info($"{file.FilenameInZip}");
            var writeStream = FileSystem.Root.OpenWrite(
                $"data/{file.FilenameInZip}",
                System.IO.FileMode.CreateNew
            );
            var success = zip.ExtractFile(file, writeStream);
            Log.Info($"{success}");
            writeStream.Close();
        }

        zip.Close();
    }

    //
    // A dock is one of those tabby floaty windows, like the console and the addon manager.
    //
    [Dock("Editor", "My Example Dock", "snippet_folder")]
    public class MyExampleDock : Widget
    {
        Color color;

        public MyExampleDock(Widget parent)
            : base(parent)
        {
            // Layout top to bottom
            SetLayout(LayoutMode.TopToBottom);

            var button = new Button("Change Color", "color_lens");
            button.Clicked = () =>
            {
                color = Color.Random;
                Update();
            };

            // Fill the top
            Layout.AddStretchCell();

            // Add a new layout cell to the bottom
            var bottomRow = Layout.Add(LayoutMode.LeftToRight);
            bottomRow.Margin = 16;
            bottomRow.AddStretchCell();
            bottomRow.Add(button);
        }

        protected override void OnPaint()
        {
            base.OnPaint();

            Paint.ClearPen();
            Paint.SetBrush(color);
            Paint.DrawRect(LocalRect);
        }
    }
}
