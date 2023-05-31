using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace CykieAppLauncher.Models;

//https://stackoverflow.com/questions/45711428/download-file-with-webclient-or-httpclient
public static class HelperUtils
{
    public static async Task DownloadFileTaskAsync(this HttpClient client, Uri uri, string fileName)
    {
        using var downloadStream = await client.GetStreamAsync(uri);
        using var fileStream = File.Create(fileName);

        await downloadStream.CopyToAsync(fileStream);
    }

    public static bool IsZipValid(string path)
    {
        try
        {
            using var zipFile = ZipFile.OpenRead(path);
            var entries = zipFile.Entries;
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}