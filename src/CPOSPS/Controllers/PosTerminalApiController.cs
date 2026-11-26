using System;
using System.Threading.Tasks;
using IIGCore.HardwareStation.PosTerminalWeb.Engine;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.Runtime.Hosting.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace IIGCore.HardwareStation.PosTerminalWeb.Controllers
{
    /// <summary>
    /// PosTerminal.Web API on the Hardware Station host:
    /// POST .../HardwareStation/api/state
    /// POST .../HardwareStation/api/ui
    /// POST .../HardwareStation/api/station
    /// </summary>
    [RoutePrefix("api")]
    public sealed class PosTerminalApiController : IController
    {
        private static readonly JsonSerializer CamelJson = JsonSerializer.Create(
            new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Include,
            });

        private static SimulatorEngine Engine
        {
            get { return PosTerminalWebStationClient.Shared.Engine; }
        }

        [HttpPost]
        public Task<JObject> state()
        {
            return Task.FromResult(JObject.FromObject(Engine.Snapshot(), CamelJson));
        }

        [HttpPost]
        public Task<JObject> ui(PosTerminalApiUiRequest request)
        {
            ThrowIf.Null(request, nameof(request));
            ThrowIf.NullOrWhiteSpace(request.Action, nameof(request.Action));

            try
            {
                return Task.FromResult(JObject.FromObject(Engine.Ui(request.Action.Trim(), request.ToBody()), CamelJson));
            }
            catch (InvalidOperationException ex)
            {
                throw new PeripheralException(PeripheralException.PeripheralEventError, ex.Message, inner: ex);
            }
        }

        [HttpPost]
        public Task<JToken> station(PosTerminalApiStationRequest request)
        {
            ThrowIf.Null(request, nameof(request));
            ThrowIf.NullOrWhiteSpace(request.Route, nameof(request.Route));

            try
            {
                var result = Engine.Station(request.Route.Trim().TrimStart('/'), request.ToBody());
                return Task.FromResult(result == null ? null : JToken.FromObject(result, CamelJson));
            }
            catch (InvalidOperationException ex)
            {
                throw new PeripheralException(PeripheralException.PeripheralEventError, ex.Message, inner: ex);
            }
        }
    }

    /// <summary>
    /// UI action payload. <see cref="Action"/> is the PosTerminal.Web <c>/api/ui/{action}</c> segment.
    /// </summary>
    public sealed class PosTerminalApiUiRequest
    {
        public string Action { get; set; }

        public string Drawer { get; set; }

        public string Number { get; set; }

        public string Type { get; set; }

        public string Label { get; set; }

        public string Track { get; set; }

        public string Month { get; set; }

        public string Year { get; set; }

        public string Value { get; set; }

        public string Field { get; set; }

        public string Name { get; set; }

        public string DeviceName { get; set; }

        public string HardwareType { get; set; }

        public string LightAction { get; set; }

        public int? LightNumber { get; set; }

        public int? Index { get; set; }

        public decimal? Weight { get; set; }

        public int? Amount { get; set; }

        public string Message { get; set; }

        public bool? Enabled { get; set; }

        public JObject ToBody()
        {
            var body = JObject.FromObject(this);
            body.Remove("Action");
            body.Remove("action");
            return body;
        }
    }

    /// <summary>
    /// Station dispatcher payload. <see cref="Route"/> is the PosTerminal.Web <c>/api/station/{*route}</c> segment.
    /// </summary>
    public sealed class PosTerminalApiStationRequest
    {
        public string Route { get; set; }

        public string DeviceName { get; set; }

        public int? Number { get; set; }

        public int? Color { get; set; }

        public int? BlinkOn { get; set; }

        public int? BlinkOff { get; set; }

        public int? Alarm { get; set; }

        public int? Amount { get; set; }

        public int? Change { get; set; }

        public string Message { get; set; }

        public string DisplayText { get; set; }

        public string Text { get; set; }

        public string Quantity { get; set; }

        public string Price { get; set; }

        public JObject ToBody()
        {
            var body = JObject.FromObject(this);
            body.Remove("Route");
            body.Remove("route");
            return body;
        }
    }
}
