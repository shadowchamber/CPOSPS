using System.Threading.Tasks;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.Runtime.Hosting.Contracts;

namespace IIGCore.HardwareStation.PosTerminalWeb.Controllers
{
    /// <summary>
    /// Light device HTTP surface that proxies to PosTerminal.Web Light/SwitchOn|SwitchOff.
    /// Aligns with the existing LightDeviceController call shape from POS.
    /// </summary>
    [RoutePrefix("PTWLightDevice")]
    public sealed class PtwLightDeviceController : IController
    {
        private static string _deviceName = PosTerminalWebConstants.Light;

        [HttpPost]
        public Task<bool> SetDeviceName(PtwLightRequest request)
        {
            ThrowIf.Null(request, nameof(request));
            ThrowIf.NullOrWhiteSpace(request.DeviceName, nameof(request.DeviceName));
            _deviceName = request.DeviceName;
            return Task.FromResult(true);
        }

        [HttpPost]
        public async Task<bool> SwitchOn(PtwLightRequest request)
        {
            var deviceName = ResolveDeviceName(request);
            await PosTerminalWebStationClient.Shared.PostAsync(
                "Light/SwitchOn",
                new
                {
                    deviceName,
                    number = request?.Number ?? 1,
                    color = request?.Color ?? 262144,
                    blinkOn = request?.BlinkOn ?? 0,
                    blinkOff = request?.BlinkOff ?? 0,
                    alarm = request?.Alarm ?? 0,
                }).ConfigureAwait(false);
            return true;
        }

        [HttpPost]
        public async Task<bool> SwitchOff(PtwLightRequest request)
        {
            var deviceName = ResolveDeviceName(request);
            await PosTerminalWebStationClient.Shared.PostAsync(
                "Light/SwitchOff",
                new { deviceName, number = request?.Number ?? 1 }).ConfigureAwait(false);
            return true;
        }

        [HttpPost]
        public Task<bool> GreenLight(PtwLightRequest request)
        {
            if (request == null)
            {
                request = new PtwLightRequest();
            }

            request.Color = 65280;
            return SwitchOn(request);
        }

        [HttpPost]
        public Task<bool> RedLight(PtwLightRequest request)
        {
            if (request == null)
            {
                request = new PtwLightRequest();
            }

            request.Color = 16711680;
            return SwitchOn(request);
        }

        [HttpPost]
        public Task<bool> OrangeLight(PtwLightRequest request)
        {
            if (request == null)
            {
                request = new PtwLightRequest();
            }

            request.Color = 16753920;
            return SwitchOn(request);
        }

        [HttpPost]
        public Task<bool> WhiteLight(PtwLightRequest request)
        {
            if (request == null)
            {
                request = new PtwLightRequest();
            }

            request.Color = 16777215;
            return SwitchOn(request);
        }

        [HttpPost]
        public Task<bool> Close(PtwLightRequest request) => SwitchOff(request);

        private static string ResolveDeviceName(PtwLightRequest request)
        {
            if (request != null && !string.IsNullOrWhiteSpace(request.DeviceName))
            {
                _deviceName = request.DeviceName;
            }

            return _deviceName;
        }
    }

    public sealed class PtwLightRequest
    {
        public string DeviceName { get; set; }

        public int? Number { get; set; }

        public int? Color { get; set; }

        public int? BlinkOn { get; set; }

        public int? BlinkOff { get; set; }

        public int? Alarm { get; set; }
    }
}
