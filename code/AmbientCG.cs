using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AmbientCG;

public enum Method
{
    PBRApproximated,
    PBRPhotogrammetry,
    PBRProcedural,
    PBRMultiAngle,
    PlainPhoto,
    ThreeDPhotogrammetry, //Should be "3D"
    HDRIStitched,
    HDRIStitchedEdited,
    UnknownOrOther
}

public enum AssetType
{
    ThreeDModel, //Should be 3D
    Atlas,
    Brush,
    Decal,
    HDRI,
    Material,
    PlainTexture,
    Substance,
    Terrain
}

public enum Sort
{
    Latest,
    Popular,
    Alphabet,
    Downloads
}

public struct SearchParameters
{
    public string Tags;
    public IEnumerable<Method> Methods;
    public IEnumerable<AssetType> Types;
    public Sort? SortType;
}

public static class Helpers
{
    public readonly static Dictionary<Method, string> MethodQuery =
        new() { { Method.ThreeDPhotogrammetry, "3DPhotogrammetry" } };

    public readonly static Dictionary<AssetType, string> AssetTypeQuery =
        new() { { AssetType.ThreeDModel, "3DModel" } };

    public readonly static List<int> Resolutions = new() { 2048, 1024, 512, 256, 128 };

    public readonly static List<string> SizeAttributes = new() { "1K", "2K", "4K", "8K", "16K" };

    public static List<string> PNGDownloadSizeAttributes =>
        SizeAttributes.Select((e) => $"{e}-PNG").ToList();

    public static List<string> PNGPreviewResolutions =>
        Resolutions.Select((e) => $"{e}-PNG").ToList();
}

public class Download
{
    public string fullDownloadPath { get; set; }
    public string downloadLink { get; set; }
    public string fileName { get; set; }
    public string filetype { get; set; }
    public string attribute { get; set; }
    public long size { get; set; }
    public List<string> zipContent { get; set; }
}

public class DownloadTypeCategory
{
    public string title { get; set; }
    public List<Download> downloads { get; set; }
}

public class DownloadFolder
{
    public string title { get; set; }
    public Dictionary<string, DownloadTypeCategory> downloadFiletypeCategories { get; set; }
}

public class ApiAsset
{
    public string assetId { get; set; }
    public string displayName { get; set; }
    public string category { get; set; }
    public string dataType { get; set; }
    public string creationMethod { get; set; }
    public Dictionary<string, string> previewImage { get; set; }
    public Dictionary<string, DownloadFolder> downloadFolders { get; set; }
}

public class ApiResponse
{
    public List<ApiAsset> foundAssets { get; set; }
    public string nextPageHttp { get; set; }
    public int numberOfResults { get; set; }
}

public class Asset
{
    // The raw asset from the api;
    public ApiAsset ApiAsset { get; protected set; }

    public string Name => ApiAsset.displayName;

    public string PreviewImage => _GetPreviewImage(); // consider caching

    public List<string> AvailableSizes => _GetAvailableSizes(); // consider caching

    private string _GetPreviewImage()
    {
        foreach (var res in Helpers.Resolutions)
        {
            var size = $"{res}-PNG";
            if (ApiAsset.previewImage.ContainsKey(size))
            {
                return ApiAsset.previewImage[size];
            }
        }

        return null;
    }

    private List<string> _GetAvailableSizes()
    {
        List<string> available = new();

        try
        {
            var zipDownloads = ApiAsset.downloadFolders["default"].downloadFiletypeCategories[
                "zip"
            ].downloads;

            foreach (var download in zipDownloads)
            {
                available.Add(download.attribute);
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to get available sizes for {ApiAsset.assetId}; {e.Message}");
        }

        return available;
    }
}

public class AmbientCGAPI
{
    public AmbientCGAPI() { }

    public async IAsyncEnumerable<string> Search(SearchParameters parameters)
    {
        var baseUrl = "https://ambientCG.com/api/v2/full_json";
        var client = new System.Net.Http.HttpClient();

        var limitPerQuery = 20;
        var queryString = BuildQueryString(parameters, limitPerQuery);

        var requestUrl = $"{baseUrl}?{queryString}";
        var assetsToRetrieve = int.MaxValue;
        var assetsRetrieved = 0;

        while (assetsToRetrieve > assetsRetrieved)
        {
            Log.Info($"Querying {requestUrl} for assets");
            var response = await client.GetAsync(requestUrl);
            var text = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<ApiResponse>(text);
            Log.Info(
                $"Found {data.foundAssets.Count} assets, out of {data.numberOfResults} total results"
            );
            assetsToRetrieve = data.numberOfResults;
            assetsRetrieved += data.foundAssets.Count;

            foreach (var asset in data.foundAssets)
            {
                yield return asset.assetId;
            }

            if (data.nextPageHttp != null)
            {
                requestUrl = data.nextPageHttp;
            }
            else
                break;
        }
    }

    private string BuildQueryString(SearchParameters parameters, int limit)
    {
        var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
        if (parameters.Tags != null)
            queryString.Add("q", parameters.Tags);

        queryString.Add("include", "downloadData,imageData,displayData");

        if (parameters.Methods != null)
            queryString.Add(
                "method",
                parameters.Methods
                    .ToList()
                    .Select(
                        (e) =>
                        {
                            if (Helpers.MethodQuery.ContainsKey(e))
                                return Helpers.MethodQuery[e];

                            return e.ToString();
                        }
                    )
                    .Aggregate((c, n) => $"{c},{n}")
            );

        if (parameters.Types != null)
            queryString.Add(
                "type",
                parameters.Types
                    .ToList()
                    .Select(
                        (e) =>
                        {
                            if (Helpers.AssetTypeQuery.ContainsKey(e))
                                return Helpers.AssetTypeQuery[e];

                            return e.ToString();
                        }
                    )
                    .Aggregate((c, n) => $"{c},{n}")
            );

        if (parameters.SortType != null)
            queryString.Add("sort", parameters.SortType.ToString());

        queryString.Add("limit", limit.ToString());

        return queryString.ToString();
    }
}
