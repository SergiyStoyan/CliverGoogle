//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Cliver
{
    //!!!COMPLETE ME!!!
    public class GooglePicker
    {
        public string Url = "http://localhost:5000/";

        public async Task<(string[] ItemIds, string AccessToken)> StartPickerWorkflowAsync(List<string> requestedFolderIds, List<string> requestedFileIds)
        {
            using (HttpListener listener = new HttpListener())
            {
                listener.Prefixes.Add(Url);
                listener.Start();
            AGAIN:
                Process.Start(new ProcessStartInfo(Url + "?folderIds=" + string.Join(",", requestedFolderIds) + "?fileIds=" + string.Join(",", requestedFileIds)) { UseShellExecute = true });

                HttpListenerContext context = await listener.GetContextAsync();

                async Task respond(string html)
                {
                    HttpListenerResponse response = context.Response;
                    byte[] buffer = Encoding.UTF8.GetBytes(html);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    response.Headers.Add("Access-Control-Allow-Origin", "*");
                    response.Headers.Add("Cache-Control", "no-store, no-cache, must-revalidate");
                    response.Headers.Add("X-Content-Type-Options", "nosniff");
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.Close();
                }
                async Task reply(string title, string message)
                {
                    await respond("<html><body><h1>" + title + "</h1><p>" + message + "</p></body></html>");
                }

                await respond(File.ReadAllText(Log.AppDir + @"\GoogleDrivePicker.html"));

                string[] itemIds = null;
                string accessToken = null;
                for (; ; )
                {
                    context = await listener.GetContextAsync();
                    var queryParameters = HttpUtility.ParseQueryString(context.Request.Url.Query);
                    itemIds = queryParameters.GetValues("itemIds");
                    if (itemIds == null)
                        continue;
                    if (itemIds.Length == 0)
                    {
                        await reply("Error!", "No item IDs were returned.");
                        break;
                    }
                    if (itemIds.Length == 1)
                        itemIds = itemIds[0].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries); // Captures "id1,id2,id3"

                    var missingIds = requestedFolderIds.Concat(requestedFileIds).Except(itemIds).ToList();
                    if (missingIds.Any())
                    {
                        string m = "The following requested item IDs has not been authorized: " + string.Join(",", missingIds);
                        Log.Error(m);
                        await reply("Error!", m + ". <br> You can close this page.");
                    }
                    else
                    {
                        accessToken = queryParameters["token"];
                        await reply("Success!", "Close this page and return to your app.");
                    }
                    break;
                }

                listener.Stop();
                return (itemIds, accessToken);
            }
        }

        /// <summary>
        /// !!!COMPLETE ME!!!
        /// </summary>
        /// <param name="requestedFolderIds"></param>
        /// <param name="requestedFileIds"></param>
        /// <exception cref="Exception"></exception>
        public async void AuthorizeAccess(string clientId, string apiKey, List<string> requestedFolderIds, List<string> requestedFileIds)
        {
            var result = await new GooglePicker().StartPickerWorkflowAsync(requestedFolderIds, requestedFileIds);
            if (string.IsNullOrEmpty(result.AccessToken))
                throw new Exception("Failed to acquire access token from Google Picker.");
        }
    }
}