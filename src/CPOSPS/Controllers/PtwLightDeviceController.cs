using System.Threading.Tasks;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.Runtime.Hosting.Contracts;

namespace IIGCore.HardwareStation.PosTerminalWeb.Controllers
{
    /// <summary>
    /// Virtual traffic-light HTTP surface. No OPOS / ServiceOPOS.
    /// POS LightDeviceManager calls this controller when the hardware-profile device name starts with PTW.
    /// </summary>
    [RoutePrefix("PTWLightDevice")]
    public sealed class PtwLightDeviceController : IController
    {
        private static string _deviceName = PosTerminalWebConstants.Light;

        private const int ColorGreen = 65280;
        private const int ColorRed = 16711680;
        private const int ColorOrange = 16753920;
        private const int ColorWhite = 16777215;

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
        public Task<bool> GreenLight(PtwLightRequest request) => SwitchOn(WithColor(request, ColorGreen));

        [HttpPost]
        public Task<bool> RedLight(PtwLightRequest request) => SwitchOn(WithColor(request, ColorRed));

        [HttpPost]
        public Task<bool> OrangeLight(PtwLightRequest request) => SwitchOn(WithColor(request, ColorOrange));

        [HttpPost]
        public Task<bool> WhiteLight(PtwLightRequest request) => SwitchOn(WithColor(request, ColorWhite));

        [HttpPost]
        public Task<bool> Close(PtwLightRequest request) => SwitchOff(request);

        [HttpPost]
        public Task<bool> BlinkOn(PtwLightRequest request)
        {
            var body = WithColor(request, ColorOrange);
            body.BlinkOn = body.BlinkOn ?? 500;
            body.BlinkOff = body.BlinkOff ?? 500;
            return SwitchOn(body);
        }

        [HttpPost]
        public Task<bool> BlinkOff(PtwLightRequest request) => GreenLight(request);

        private static PtwLightRequest WithColor(PtwLightRequest request, int color)
        {
            if (request == null)
            {
                request = new PtwLightRequest();
            }

            request.Color = color;
            return request;
        }

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
