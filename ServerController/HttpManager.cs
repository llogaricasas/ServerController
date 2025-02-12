using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Diagnostics;

namespace ServerController
{
    public class HttpManager
    {
        async Task ProcessRequestAsync(HttpListenerContext context)
        {
            //TODO: some kind of request security check here
            //
            //
            //information will be stored in the body in json format, use the body to determine what kind of command the request is
            context.Response.Headers.Add("Content-Type", "application/json");
            //try and parse the incoming webrequest
            JsonDocument body = null;
            try
            {
                string rqBody;
                using (Stream receiveStream = context.Request.InputStream)
                {
                    using (StreamReader readStream = new StreamReader(receiveStream, context.Request.ContentEncoding))
                    {
                        rqBody = await readStream.ReadToEndAsync();
                    }
                }
                if (Program.verbose)
                {
                    Console.WriteLine(rqBody);
                }
                body = JsonDocument.Parse(rqBody);
            }
            catch (JsonException exception)
            {
                //if the json isn't valid return as an exception
                Console.Error.Write(exception.Message);
                //send a failed webresponse
                Utf8JsonWriter outResponse = new Utf8JsonWriter(context.Response.OutputStream);
                outResponse.WriteStartObject();
                outResponse.WriteBoolean("success", false);
                outResponse.WriteEndObject();
                outResponse.Flush();
                context.Response.Close();
                return;
            }

            JsonElement property;
            if (!body.RootElement.TryGetProperty("command", out property) && Program.verbose)
            {
                Console.WriteLine("Unrecognised request");
            }

            Utf8JsonWriter outgoingResponse = new Utf8JsonWriter(context.Response.OutputStream);
            outgoingResponse.WriteStartObject();
            bool success = true;
            string id;
            ushort port;
            //figure out what operation to perform on the server instances
            switch (property.GetString())
            {
                case "insert":
                    //add new room
                    if (!body.RootElement.TryGetProperty("id", out property))
                    {
                        success = false;
                        break;
                    }
                    id = property.GetString();
                    port = Program.AddServer(id);
                    outgoingResponse.WriteNumber("port", port);
                    if (port == 0)
                    {
                        success = false;
                    }
                    break;
                case "remove":
                    //delete specified room 
                    if (!body.RootElement.TryGetProperty("id", out property))
                    {
                        success = false;
                        break;
                    }
                    id = property.GetString();
                    success = Program.RemoveServer(id);
                    break;
                case "query":
                    //try and match a room id and return it's port
                    if (!body.RootElement.TryGetProperty("id", out property))
                    {
                        success = false;
                        break;
                    }
                    id = property.GetString();
                    //find server process with matching id
                    Process runningServer;
                    if (Program.ServerIdInstancePairs.TryGetValue(id, out runningServer))
                    {
                        //grab the port in the properties list
                        for (int it = 0; it < Program.AssignedPorts.Count; it++)
                        {
                            if (Program.AssignedPorts[it].attachedProcess == runningServer)
                            {
                                outgoingResponse.WriteNumber("port", Program.AssignedPorts[it].port);
                                break;
                            }
                        }
                    }
                    else
                    {
                        success = false;
                    }
                    break;
                default:
                    success = false;
                    break;
            }

            outgoingResponse.WriteBoolean("success", success);
            outgoingResponse.WriteEndObject();
            outgoingResponse.Flush();
            //send the response
            context.Response.Close();
        }

        //task manager to handle incoming webrequests from the magi-chat.net host
        public async Task Listen(string prefix, int maxConcurrentRequests, CancellationToken token)
        {
            //set up the http listener and run concurrent tasks to handle multiple requests
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            //set up a number of concurrent listen tasks equal to the max amount of incoming requests
            var requests = new HashSet<Task>();
            for (int it = 0; it < maxConcurrentRequests; it++)
            {
                requests.Add(listener.GetContextAsync());
            }

            //run these tasks until told to cancel
            while (!token.IsCancellationRequested)
            {
                Task t = await Task.WhenAny(requests);
                requests.Remove(t);

                if (t is Task<HttpListenerContext>)
                {
                    //when a listen task is completed, process the webrequest and add a new listen task to the pool
                    var context = (t as Task<HttpListenerContext>).Result;
                    requests.Add(ProcessRequestAsync(context));
                    requests.Add(listener.GetContextAsync());
                }
            }
        }
    }
}
