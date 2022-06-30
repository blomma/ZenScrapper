using System.Net;

using HtmlAgilityPack;

if (args.Length == 0)
{
    throw new Exception("Missing folder for storing comics in");
}

var storeComicsIn = args[0];

HttpClient client = new();
var response = await client.GetStringAsync("https://www.zenpencils.com");

var doc = new HtmlDocument();
doc.LoadHtml(response);

var comicPageUrls = doc.DocumentNode.Descendants("div")
            .Where(w => w.Id == "ceo_comic_list_dropdown_widget-2")
            .SelectMany(s => s.Descendants("option")
                .Where(d => d.Attributes["class"].Value == "level-0")
            )
            .Select(s => new Tuple<string?, string?>(
                s.ChildAttributes("value").Select(s => s.Value).FirstOrDefault(),
                s.InnerText
                ))
            ;

var invalidFileNameChars = Path.GetInvalidFileNameChars().ToList();
invalidFileNameChars.Add(':');

foreach (var comicPageUrl in comicPageUrls)
{
    if (comicPageUrl.Item1 == "https://www.zenpencils.com/comic/welcome/")
    {
        continue;
    }

    if (comicPageUrl == null)
    {
        throw new Exception($"Missing comicPageUrl");
    }

    response = await client.GetStringAsync(comicPageUrl.Item1);
    doc = new HtmlDocument();
    doc.LoadHtml(response);

    var imageUrls = doc.DocumentNode.Descendants("div")
        .Where(d => d.Id == "comic")
        .SelectMany(s => s.Descendants("img"))
        .SelectMany(s => s.ChildAttributes("src"))
        .Select(s => s.Value);

    if (!imageUrls.Any())
    {
        throw new Exception($"Missing images for comicPage:{comicPageUrl}");
    }

    var postTitle = WebUtility.HtmlDecode(comicPageUrl.Item2);
    if (string.IsNullOrWhiteSpace(postTitle))
    {
        throw new Exception($"Missing postTitle for comicPage:{comicPageUrl}");
    }

    foreach (var invalidChar in invalidFileNameChars)
    {
        var replacement = invalidChar == ':' ? '.' : '_';
        postTitle = postTitle.Replace(invalidChar, replacement);
    }

    var comicDir = $"{storeComicsIn}/{postTitle}";

    var dirInfo = Directory.CreateDirectory(comicDir);

    foreach (var imageUrl in imageUrls)
    {
        var imageURI = new Uri($"https:{imageUrl}");
        var imageName = imageURI.Segments.LastOrDefault();
        if (string.IsNullOrWhiteSpace(imageName))
        {
            throw new Exception($"Missing imageName for comicPage:{comicPageUrl}, imageUrl: {imageUrl}");
        }

        var imageBytes = await client.GetByteArrayAsync(imageURI);

        var imageFileName = $"{comicDir}/{imageName}";
        await File.WriteAllBytesAsync(imageFileName, imageBytes);
    }

    Console.WriteLine($"{postTitle}: Wrote {imageUrls.Count()} images for for comicPage:{comicPageUrl}");
}