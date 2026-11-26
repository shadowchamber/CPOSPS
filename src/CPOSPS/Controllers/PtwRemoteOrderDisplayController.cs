using System.Threading.Tasks;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.Runtime.Hosting.Contracts;

namespace IIGCore.HardwareStation.PosTerminalWeb.Controllers
{
    /// <summary>
    /// Remote order display HTTP surface → PosTerminal.Web RemoteOrderDisplay/* routes.
    /// </summary>
    [RoutePrefix("PTWRemoteOrderDisplay")]
    public sealed class PtwRemoteOrderDisplayController : IController
    {
        [HttpPost]
        public async Task<bool> DisplayMessage(PtwRemoteOrderRequest request)
        {
            ThrowIf.Null(request, nameof(request));
            await PosTerminalWebStationClient.Shared.PostAsync(
                "RemoteOrderDisplay/DisplayMessage",
                new
                {
                    deviceName = string.IsNullOrWhiteSpace(request.DeviceName)
                        ? PosTerminalWebConstants.RemoteOrderDisplay
                        : request.DeviceName,
                    message = request.Message ?? string.Empty,
                }).ConfigureAwait(false);
            return true;
        }

        [HttpPost]
        public async Task<bool> ClearDisplay(PtwRemoteOrderRequest request)
        {
            var deviceName = request != null && !string.IsNullOrWhiteSpace(request.DeviceName)
                ? request.DeviceName
                : PosTerminalWebConstants.RemoteOrderDisplay;
            await PosTerminalWebStationClient.Shared.PostAsync(
                "RemoteOrderDisplay/ClearDisplay",
                new { deviceName }).ConfigureAwait(false);
            return true;
        }
    }

    public sealed class PtwRemoteOrderRequest
    {
        public string DeviceName { get; set; }

        public string Message { get; set; }
    }
}
