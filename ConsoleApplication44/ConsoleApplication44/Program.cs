using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.IO;
using System.Reflection;

namespace ConsoleApplication44
{

    public class Converter
    {
        private string serializerFormat;
        private readonly List<ISerializer> serializers = new List<ISerializer>();

        public string GetFormat()
        {
            serializerFormat = Console.ReadLine();
            return Console.ReadLine();
        }

        public Converter()
        {
            serializers.Add(new JsonSerializer());
        }

        public void SetFormat(string type)
        {
            this.serializerFormat = type;
        }

        public ISerializer GetSerializer(string serializerFormat)
        {
            return serializers.First(x => x.Satisfy(serializerFormat));
        }

        public Input GetInputObject(string serializedString)
        {
            return GetSerializer(serializerFormat).Deserialize<Input>(serializedString);
        }

        public Output GetOutputObject(Input input)
        {
            var output = new Output();

            foreach (var sum in input.Sums)
            {
                output.SumResult += sum;
            }
            output.SumResult *= input.K;

            output.MulResult = 1;
            foreach (var mul in input.Muls)
            {
                output.MulResult *= mul;
            }

            output.SortedInputs = input.Sums.Concat(input.Muls.Select(x => (decimal)x)).ToArray();
            Array.Sort(output.SortedInputs);

            return output;
        }

        public string GetSerializedOutput(Output output)
        {
            return GetSerializer(serializerFormat).Serialize(output)
                .Replace("\n", "").Replace("\t", "").Replace(" ", "");
        }
    }

    public interface ISerializer
    {
        string Serialize<T>(T obj);
        T Deserialize<T>(string bytes);
        bool Satisfy(string serializerFormat);
    }

    public class JsonSerializer : ISerializer
    {

        public string Serialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public T Deserialize<T>(string bytes)
        {
            return JsonConvert.DeserializeObject<T>(bytes);
        }

        public bool Satisfy(string serializerFormat)
        {
            return serializerFormat == "Json";
        }
    }

    class ServerBase
    {
        private readonly HttpListener server = new HttpListener();
        private bool isNeedFinish = false;
        private HttpListenerContext serverContext;

        public string Url { get; set; }
        public int Port { get; set; }

        public void Start(string[] methodNames, Dictionary<string, MethodInfo> methods)
        {
            foreach (var method in methodNames)
            {
                server.Prefixes.Add($"{Url}:{Port}/{method}/");
            }

            server.Start();
            while (true)
            {
                try
                {
                    if (isNeedFinish) return;
                    serverContext = server.GetContext();
                    serverContext.Response.StatusCode = (int)HttpStatusCode.OK;

                    foreach (var method in methodNames)
                    {
                        if (serverContext.Request.RawUrl.Contains(method))
                        {
                            methods[method].Invoke(this, null);
                            break;
                        }
                    }
                }
                catch (Exception e)
                {

                }
            }
        }

        public string GetData()
        {
            var reader = new StreamReader(serverContext.Request.InputStream);
            return reader.ReadToEnd();
        }

        public void WriteMessage(string message)
        {
            serverContext.Response.ContentLength64 = Encoding.UTF8.GetByteCount(message);
            serverContext.Response.KeepAlive = false;
            using (Stream stream = serverContext.Response.OutputStream)
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(message);
                }
            }
        }

        public void StopServer()
        {
            isNeedFinish = true;
            server.Stop();
        }
    }

    class Server : ServerBase
    {
        private Input input = null;
        private readonly Converter converter = new Converter();
        private readonly JsonSerializer serializer = new JsonSerializer();

        private readonly string[] methods = { "Ping", "PostInputData", "GetAnswer", "Stop", "GetInputData", "WriteAnswer" };
        private readonly Dictionary<string, MethodInfo> serverMethods = new Dictionary<string, MethodInfo>();

        public Server()
        {
            converter.SetFormat("Json");
            foreach (var method in methods)
            {
                serverMethods[method] = typeof(Server).GetMethod(method);
            }
        }

        public void StartServer()
        {
            Start(methods, serverMethods);
        }

        public void WriteAnswer()
        {
            var serializedInput = GetData();
            WriteMessage("");
            Console.WriteLine("Get answer:" + serializedInput);
        }

        public void GetInputData()
        {
            var input = new Input() { K = 10, Muls = new[] { 1, 4 }, Sums = new decimal[] { 1.01m, 2.02m } };
            WriteMessage(serializer.Serialize(input));
            Console.WriteLine("Input object sended");
        }

        public void Stop()
        {
            WriteMessage("");
            StopServer();
        }

        public void Ping()
        {
            WriteMessage("");
        }

        public void GetAnswer()
        {
            if (input == null)
            {
                WriteMessage("");
                return;
            }
            var output = converter.GetOutputObject(input);
            var serializerOutput = converter.GetSerializedOutput(output);
            WriteMessage(serializerOutput);
        }

        public void PostInputData()
        {
            var serializedInput = GetData();
            input = serializer.Deserialize<Input>(serializedInput);
            WriteMessage("");
        }
    }


    public class Output
    {
        public decimal SumResult { get; set; }
        public int MulResult { get; set; }
        public decimal[] SortedInputs { get; set; }

    }

    public class Input
    {
        public int K { get; set; }
        public decimal[] Sums { get; set; }
        public int[] Muls { get; set; }

    }



    class Program
    {
        static void Main(string[] args)
        {
            var server = new Server();
            server.Url = "http://127.0.0.1";
            server.Port = GetPort();
            server.StartServer();
        }

        static int GetPort()
        {
            return int.Parse(Console.ReadLine());
        }
    }
}
