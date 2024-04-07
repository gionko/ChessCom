using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace ChessCom
{
    internal class ChessApi
    {
        /// <summary>
        /// La queue che contiene tutte le chiamate da fare a api.chess.com
        /// </summary>
        private static Queue<Api> _APIQueue = new Queue<Api>();

        /// <summary>
        /// Variable di lock di <see cref="_APIQueue"/>
        /// </summary>
        private static readonly object _APIQueueLock = new object();

        /// <summary>
        /// La lista di tutte le API processate da <see cref="RequestProcessor"/> complete
        /// dei relativi risultati.
        /// </summary>
        private static List<Api> _ProcessedAPIsList = new List<Api>();

        /// <summary>
        /// Variabile di lock di <see cref="_ProcessedAPIsList"/>
        /// </summary>
        private static readonly object _ProcessedAPIsLock = new object();

        /// <summary>
        /// Aggiunge un nuovo oggetto <see cref="ChessApi.Api"/> alla query per essere elaborata.
        /// Questo metodo è threadsafe.
        /// </summary>
        /// <param name="api"></param>
        public static void Enqueue(ChessApi.Api api)
        {
            lock(_APIQueueLock)
            {
                _APIQueue.Enqueue(api);
            }
        }

        /// <summary>
        /// Estrae l'oggetto <see cref="ChessApi.Api"/> da elaborare dalla queue <see cref="_APIQueue"/>. Questo metodo è threadsafe.
        /// </summary>
        /// <returns><see cref="ChessApi.Api"/> e lo toglie dalla queue <see cref="_APIQueue"/>, se la queue è vuota <see cref="null"/></returns>
        public static ChessApi.Api Dequeue()
        {
            ChessApi.Api api = null;
            lock(_APIQueueLock)
            {
                if (_APIQueue.Count > 0)
                {
                    api = _APIQueue.Dequeue();
                }
            }
            return api;
        }

        /// <summary>
        /// Ottiene il numero di oggetti <see cref="ChessApi.Api"/> ancora da elaborare nella <see cref="_APIQueue"/>.
        /// E' comodo per sapere se ci sono ancora richieste in sospedo. Questo metodo è threadsafe.
        /// </summary>
        /// <returns>Il numero di oggetti nella queue <see cref="_APIQueue"/></returns>
        public static int GetRequestCount()
        {
            lock (_APIQueueLock)
            {
                return _APIQueue.Count;
            }
        }

        /// <summary>
        /// Ottiene il conteggio di <see cref="Api"/> nella lista <see cref="_ProcessedAPIsLock"/>
        /// Questo metodo è Threadsafe
        /// </summary>
        /// <returns></returns>
        public static int GetProcessedRequestCount()
        {
            lock (_ProcessedAPIsLock)
            {
                return _ProcessedAPIsList.Count;
            }
        }

        /// <summary>
        /// Ottiene il numero di <see cref="HttpStatusCode.TooManyRequests"/> ottenuti da <see cref="CallChessCom(Api)"/>
        /// </summary>
        /// <returns></returns>
        public static int GetTooManyConnectionCount()
        {
            lock (_ProcessedAPIsLock)
            {
                return _ProcessedAPIsList.Where(x => x.HttpStatusCode == HttpStatusCode.TooManyRequests).Count();
            }
        }

        public static TimeSpan GetAverageExecutionTime()
        {
            long avgTime = 0;

            lock (_ProcessedAPIsLock)
            {
                if (_ProcessedAPIsList.Count == 0)
                    return TimeSpan.Zero;

                for (int i = 0; i < _ProcessedAPIsList.Count; i++)
                    avgTime += _ProcessedAPIsList[i].ExecutionTime;

                avgTime = avgTime / _ProcessedAPIsList.Count;
            }

            return new TimeSpan(avgTime);
        }

        /// <summary>
        /// Aggiunge <see cref="ChessApi.Api"/> alla lista <see cref="_ProcessedAPIsList"/>
        /// Questo metodo è threadsafe.
        /// </summary>
        /// <param name="api"></param>
        private static void AddToList(ChessApi.Api api)
        {
            lock (_ProcessedAPIsLock)
            {
                _ProcessedAPIsList.Add(api);
            }
        }

        /// <summary>
        /// Metodo d'avvido di <see cref="RequestProcessor()"/> sottoforma di thread
        /// </summary>
        public static void StartRequestProcessor()
        {
            Thread reqProcessor = new Thread(RequestProcessor);
            reqProcessor.Start();
        }

        /// <summary>
        /// Thread che viene esequito per processare la queue <see cref="_APIQueue"/>
        /// </summary>
        private static async void RequestProcessor()
        {
            while (true)
            {
                var queueCount = GetRequestCount();

                if (queueCount == 0) 
                { 
                    Thread.Sleep(50);
                    continue;
                }

                if (queueCount > 0)
                {
                    var api = ChessApi.Dequeue();
                    await CallChessCom(api);
                    continue;
                }
            }
        }

        /// <summary>
        /// Il metodo che effettivamente contatta api.chess.com ed elabora la risposta
        /// </summary>
        /// <param name="api"></param>
        /// <returns></returns>
        private static async Task CallChessCom(Api api)
        {
            var startTime = DateTime.Now;

            using (HttpClient httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.GZip }))
            {
                string url = api.HttpCall;

                //WARNING: le protezioni cloudfare di chess.com richiedono l'impostazione di httpClient.
                string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36 Edg/123.0.0.0";
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                // Eseguiamo la chiamata http GET a "url"
                HttpResponseMessage response = await httpClient.GetAsync(url);

                // Troppe richieste... in questo caso rimettiamo in queue la richiesta http e attendiamo un attimo.
                // Viene aggiunto il risultato lo stesso alla lista, solo con JsonToken = null
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    ChessApi.Enqueue(api);
                    api.SetStatusCode(response.StatusCode);
                    api.SetJsonToken(null);
                    api.SetExecutionTime(startTime);
                    AddToList(api);
                    return;
                }

                // Tutto ok, possiamo processare la risposta
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var jsonToken = JObject.Parse(jsonResponse);
                    api.SetJsonToken(jsonToken);
                    api.SetStatusCode(response.StatusCode);
                    api.SetExecutionTime(startTime);
                    AddToList(api);

                    if (jsonToken["players"] is JArray playersArray)
                    {
                        foreach (var player in playersArray)
                        {
                            string urlpl = "https://api.chess.com/pub/player/" + player + "/stats";
                            Enqueue(new Api(urlpl));
                        }
                    }

                    return;
                }

                // I dati a questo url non sono più disponibili... e non lo saranno mai +!
                // questo url non può più essere usato.
                if (response.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    ChessApi.Enqueue(api);
                    api.SetStatusCode(response.StatusCode);
                    api.SetJsonToken(null);
                    api.SetExecutionTime(startTime);
                    AddToList(api);
                    return;
                }

                return;
            }
        }

        /// <summary>
        /// Sono le chiamate a chess.com, da inseire nella comoda queue <see cref="_APIQueue"/>
        /// Questa classe supporta la serializzazione in JSON
        /// </summary>
        public class Api
        {
            /// <summary>
            /// L'url eseguito su https://api.chess.com/
            /// </summary>
            [JsonProperty("HttpCall")]
            public string HttpCall {  get; private set; }

            /// <summary>
            /// La risposta ottenuta da api.chess.com
            /// </summary>
            [JsonProperty("JToken")]
            public JToken JsonToken { get; private set; }

            [JsonProperty("HttpStatusCode")]
            public HttpStatusCode HttpStatusCode { get; private set; }
            
            /// <summary>
            /// Tempo di esecuzione della chiamata Http per soddisfare la richiesta
            /// Il valore è espresso in TimeSpan Tick
            /// </summary>
            [JsonProperty("ExecutionTime")]
            public long ExecutionTime { get; private set; }

            /// <summary>
            /// Costruttore di default
            /// </summary>
            /// <param name="url">l'url di chiamata a api.chess.com</param>
            public Api(string url)
            {
                HttpCall = url;
            }

            /// <summary>
            /// Imposta la proprietà <see cref="JsonToken"/>
            /// </summary>
            /// <param name="jsonToken"></param>
            public void SetJsonToken(JToken jsonToken)
            {
                JsonToken = jsonToken;
            }

            public void SetStatusCode(HttpStatusCode statusCode)
            {
                HttpStatusCode = statusCode;
            }


            public void SetExecutionTime(DateTime startTime)
            {
                ExecutionTime = new TimeSpan(DateTime.Now.Ticks - startTime.Ticks).Ticks;
            }
        }
    }
}
