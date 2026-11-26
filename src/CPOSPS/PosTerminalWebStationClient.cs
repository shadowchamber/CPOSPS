using System;
using System.Threading;
using System.Threading.Tasks;
using IIGCore.HardwareStation.PosTerminalWeb.Engine;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IIGCore.HardwareStation.PosTerminalWeb
{
    /// <summary>
    /// In-process PosTerminal station API (<c>SimulatorEngine.Station</c>).
    /// </summary>
    public sealed class PosTerminalWebStationClient : IDisposable
    {
        private static readonly object Sync = new object();
        private static PosTerminalWebStationClient _shared;

        private readonly SimulatorEngine _engine;

        public PosTerminalWebStationClient(PosTerminalWebOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.ContentRoot))
            {
                throw new ArgumentException("ContentRoot is required.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.OposFolder))
            {
                throw new ArgumentException("OposFolder is required.", nameof(options));
            }

            _engine = new SimulatorEngine(
                options.ContentRoot,
                options.OposFolder,
                options.WatchOpos,
                seedDefaults: true);
        }

        public static PosTerminalWebStationClient Shared
        {
            get
            {
                if (_shared != null)
                {
                    return _shared;
                }

                lock (Sync)
                {
                    if (_shared == null)
                    {
                        _shared = new PosTerminalWebStationClient(PosTerminalWebOptions.FromEnvironment());
                    }

                    return _shared;
                }
            }
        }

        public SimulatorEngine Engine
        {
            get { return _engine; }
        }

        public Task<T> PostAsync<T>(string route, object body, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = PostRaw(route, body);
            if (json == null)
            {
                return Task.FromResult(default(T));
            }

            if (typeof(T) == typeof(JToken) || typeof(T) == typeof(JObject) || typeof(T) == typeof(JArray))
            {
                return Task.FromResult((T)(object)json);
            }

            if (typeof(T) == typeof(string))
            {
                if (json.Type == JTokenType.String)
                {
                    return Task.FromResult((T)(object)json.ToString());
                }

                return Task.FromResult((T)(object)json.ToString(Formatting.None));
            }

            if (typeof(T) == typeof(bool))
            {
                return Task.FromResult((T)(object)json.Value<bool>());
            }

            if (typeof(T) == typeof(decimal))
            {
                return Task.FromResult((T)(object)json.Value<decimal>());
            }

            if (typeof(T) == typeof(int))
            {
                return Task.FromResult((T)(object)json.Value<int>());
            }

            return Task.FromResult(json.ToObject<T>());
        }

        public Task PostAsync(string route, object body, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PostRaw(route, body);
            return Task.CompletedTask;
        }

        public Task<string> PostForTokenAsync(string route, object body, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = PostRaw(route, body);
            if (json == null)
            {
                return Task.FromResult<string>(null);
            }

            if (json.Type == JTokenType.String)
            {
                return Task.FromResult(json.Value<string>());
            }

            var token = json["token"] ?? json["Token"];
            return Task.FromResult(token != null ? token.ToString() : json.ToString(Formatting.None));
        }

        private JToken PostRaw(string route, object body)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                throw new ArgumentException("Station route is required.", nameof(route));
            }

            var payload = body == null
                ? new JObject()
                : JToken.FromObject(body);

            try
            {
                var result = _engine.Station(route.TrimStart('/'), payload);
                if (result == null)
                {
                    return null;
                }

                return JToken.FromObject(result);
            }
            catch (InvalidOperationException ex)
            {
                throw new PeripheralException(
                    PeripheralException.PeripheralEventError,
                    ex.Message,
                    inner: ex);
            }
        }

        public void Dispose()
        {
            _engine.Opos.Dispose();
        }
    }
}
