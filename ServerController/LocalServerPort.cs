using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Text.Json;
using System.Net.Http;
using System.IO;

namespace ServerController
{
    class LocalServerPort
    {
        //class to handle incoming requests from running server instances 
        public async Task Listen(string prefix, CancellationToken token)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context;
                context = await listener.GetContextAsync();

                //receive the new server properties, send the confimation response
                context.Response.Headers.Add("Content-Type", "application/json");

                //local function to send unsuccessful webresponse
                void SendBad()
                {
                    Utf8JsonWriter outResponse = new Utf8JsonWriter(context.Response.OutputStream);
                    outResponse.WriteStartObject();
                    outResponse.WriteBoolean("success", false);
                    outResponse.WriteEndObject();
                    context.Response.Close();
                }

                //try and parse the incoming webrequest
                try
                {
                    using (Stream stream = context.Request.InputStream)
                    {
                        using (StreamReader readStream = new StreamReader(stream, context.Request.ContentEncoding))
                        {

                        }
                    }
                }
                catch
                {

                }
            }
        }


    }
}
