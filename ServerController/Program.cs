using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

namespace ServerController
{
    class Program
    {
        static readonly HttpClient client = new HttpClient();
        public static List<Process> ServerInstances;
        public static Dictionary<string, Process> ServerIdInstancePairs;
        public static List<ServerProperties> AssignedPorts;

        public class ServerProperties
        {
            public Process attachedProcess;
            public ushort port;
            public int playerCount;
        }

        //static args
        public static readonly ushort StartPort = 2020;
        public static readonly ushort maxServers = 32;
        public static readonly int maxThreads = 16;
        public static string processAdress;
        public static bool verbose = false;

        static void Main(string[] args)
        {
            int threadCount = 4;
            //define the command arguments and what the commands do
            for (int it = 0; it < args.Length; it++)
            {
                switch (args[it])
                {
                    case "--path":
                    case "--p":
                    case "-path":
                    case "-p":
                        if (it < args.Length - 1)
                        {
                            processAdress = args[it + 1];
                        }
                        break;
                    case "-v":
                    case "-verbose":
                    case "--v":
                    case "--verbose":
                        verbose = true;
                        break;
                    case "--c":
                    case "--count":
                    case "-count":
                    case "-c":
                        if (it < args.Length - 1)
                        {
                            int newThreadCount = int.MaxValue;
                            if (int.TryParse(args[it + 1], out newThreadCount))
                            {
                                if (newThreadCount <= maxThreads)
                                {
                                    threadCount = newThreadCount;
                                }
                            }
                        }
                        break;
                }
            }


            //check that we have a valid process adress
            if (!File.Exists(processAdress))
            {
                Console.WriteLine("Passed in address: " + processAdress + "doesn't exist");
                return;
            }

            if (verbose)
            {
                Console.WriteLine("File: " + processAdress + " exists");
            }

            //set up a cancellation token
            CancellationTokenSource stopHttp = new CancellationTokenSource();

            ServerIdInstancePairs = new Dictionary<string, Process>();
            ServerInstances = new List<Process>();
            AssignedPorts = new List<ServerProperties>(maxServers);

            //fill up the properties array with process port pairs to allow dynamic port switching on the server
            for (ushort it = 0; it < maxServers; it++)
            {
                ServerProperties properties = new ServerProperties();
                properties.port = (ushort)(StartPort + it);
                properties.attachedProcess = null;
                AssignedPorts.Add(properties);
            }

            //initiate networking thread to handle webrequests
            HttpManager manager = new HttpManager();
            Task httpThread = manager.Listen("http://*:8080/", threadCount, stopHttp.Token);
            //Task httpThread = manager.Listen("https://localhost:8080/", threadCount, stopHttp.Token);

            //clean up and finish
            httpThread.Wait();
        }

        //kill the server process and free up any of it's resources
        public static bool RemoveServer(string id)
        {
            //try to remove the server with the specified roomid
            if (ServerIdInstancePairs.ContainsKey(id))
            {
                Process processToKill = ServerIdInstancePairs[id];

                //remove any reference to the process
                ServerIdInstancePairs.Remove(id);
                ServerInstances.Remove(processToKill);

                ServerProperties propertiesToKill = null;

                //find the port the process is tethered to
                for (int it = 0; it < AssignedPorts.Count; it++)
                {
                    if (AssignedPorts[it].attachedProcess == processToKill)
                    {
                        propertiesToKill = AssignedPorts[it];
                        break;
                    }
                }

                //kill the process
                processToKill.Kill();

                //free up it's port
                if (propertiesToKill != null)
                {
                    propertiesToKill.attachedProcess = null;
                }

                if (verbose)
                {
                    Console.WriteLine("Killed server with id: " + id);
                }

                return true;
            }
            else
            {
                if (verbose)
                {
                    Console.WriteLine("Failed to kill server with id: " + id);
                }
                return false;
            }
        }


        public static ushort AddServer(string id)
        {
            //add a new server instance and return the assigned port number
            if (ServerInstances.Count >= maxServers)
            {
                return 0;
            }

            //if there is already an existing server with the passed in id, return the port of the existing instance
            if (ServerIdInstancePairs.ContainsKey(id))
            {
                for (int it = 0; it < AssignedPorts.Count; it++)
                {
                    if (ServerIdInstancePairs[id] == AssignedPorts[it].attachedProcess)
                    {
                        return AssignedPorts[it].port;
                    }
                }
            }

            for (int it = 0; it < AssignedPorts.Count; it++)
            {
                if (AssignedPorts[it].attachedProcess == null)
                {
                    //create a new server process with the current port
                    Process newServer;
                    try
                    {
                        newServer = Process.Start(processAdress, "-id " + id + " -port " + AssignedPorts[it].port.ToString());
                        
                    }
                    catch (Exception exception)
                    {
                        if (verbose)
                        {
                            Console.WriteLine(exception.Message);
                        }
                        return 0;
                    }

                    if (newServer == null)
                    {
                        if (verbose)
                        {
                            Console.WriteLine("Unable to start server process, object is null");
                        }
                        return 0;
                    }

                    if (verbose)
                    {
                        Console.WriteLine("Created new server with id: " + id + " port: " + AssignedPorts[it].port.ToString());
                    }

                    //successfully created the new server instance, return the new port
                    ServerInstances.Add(newServer);
                    ServerIdInstancePairs.Add(id, newServer);
                    AssignedPorts[it].attachedProcess = newServer;
                    return AssignedPorts[it].port;
                }
            }

            //no available ports found
            return 0;
        }
    }
}