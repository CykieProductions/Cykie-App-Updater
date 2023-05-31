using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;

namespace CykieAppLauncher.Models;

//https://stackoverflow.com/questions/45711428/download-file-with-webclient-or-httpclient
public static class HttpClientUtils
{
    public static async Task DownloadFileTaskAsync(this HttpClient client, Uri uri, string fileName)
    {
        using (var downloadStream = await client.GetStreamAsync(uri))
        {
            using (var fileStream = File.Create(fileName))
            {
                await downloadStream.CopyToAsync(fileStream);
            }
        }
    }
}