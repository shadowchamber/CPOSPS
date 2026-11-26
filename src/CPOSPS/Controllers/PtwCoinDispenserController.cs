using System.Threading.Tasks;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.Runtime.Hosting.Contracts;

namespace IIGCore.HardwareStation.PosTerminalWeb.Controllers
{
    /// <summary>
    /// Coin dispenser HTTP surface → PosTerminal.Web CoinDispenser/DispenseChange.
    /// </summary>
    [RoutePrefix("PTWCoinDispenser")]
    public sealed class PtwCoinDispenserController : IController
    {
        [HttpPost]
        public async Task<bool> DispenseChange(PtwCoinDispenseRequest request)
        {
            ThrowIf.Null(request, nameof(request));
            await PosTerminalWebStationClient.Shared.PostAsync(
                "CoinDispenser/DispenseChange",
                new
                {
                    deviceName = string.IsNullOrWhiteSpace(request.DeviceName)
                        ? PosTerminalWebConstants.CoinDispenser
                        : request.DeviceName,
                    amount = request.Amount,
                    change = request.Amount,
                }).ConfigureAwait(false);
            return true;
        }
    }

    public sealed class PtwCoinDispenseRequest
    {
        public string DeviceName { get; set; }

        public int Amount { get; set; }
    }
}
