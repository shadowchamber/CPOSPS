using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Dynamics.Commerce.HardwareStation.Peripherals;
using Microsoft.Dynamics.Commerce.Runtime.Handlers;
using Microsoft.Dynamics.Commerce.Runtime.Messages;

namespace IIGCore.HardwareStation.PosTerminalWeb.Handlers
{
    /// <summary>
    /// Shared helpers for OPOS-chain bridges that activate only when DeviceName starts with PTW.
    /// </summary>
    /// <remarks>
    /// Handler instances are not guaranteed sticky across Open → Print. Print/Close omit DeviceName,
    /// so active PTW names are stored in a static map keyed by peripheral kind. Otherwise the bridge
    /// ExecuteNext's back into CustomOpos* → stack overflow (0xc00000fd) / IIS w3wp crash.
    /// </remarks>
    public static class PtwOposNameBridge
    {
        private static readonly ConcurrentDictionary<string, string> ActiveByKind =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsPtwName(string deviceName)
        {
            return !string.IsNullOrWhiteSpace(deviceName)
                && deviceName.StartsWith("PTW", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPtwDevice(object request, string kind)
        {
            var active = GetActive(kind);
            return IsPtwName(PtwDeviceName.From(request, active));
        }

        public static string GetActive(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                return null;
            }

            return ActiveByKind.TryGetValue(kind, out var name) ? name : null;
        }

        /// <summary>
        /// Remembers PTW device name on Open/Lock; clears on Close/Unlock.
        /// Callers that route on IsPtwDevice MUST peek GetActive before calling this —
        /// Close clears the map, and ExecuteNext(OPOS) after a cleared PTW Close stack-overflows IIS.
        /// </summary>
        public static void TrackSession(object request, string kind)
        {
            if (request == null || string.IsNullOrWhiteSpace(kind))
            {
                return;
            }

            var typeName = request.GetType().Name ?? string.Empty;
            bool isOpen = typeName.StartsWith("Open", StringComparison.Ordinal)
                || typeName.StartsWith("Lock", StringComparison.Ordinal);
            bool isClose = typeName.StartsWith("Close", StringComparison.Ordinal)
                || typeName.StartsWith("Unlock", StringComparison.Ordinal)
                || typeName.StartsWith("UnLock", StringComparison.Ordinal);

            var fromRequest = PtwDeviceName.From(request);
            if (isOpen && IsPtwName(fromRequest))
            {
                ActiveByKind[kind] = fromRequest;
                return;
            }

            if (isClose)
            {
                ActiveByKind.TryRemove(kind, out _);
            }
        }

        public static Task<Response> ExecuteNextAsync(INamedRequestHandlerAsync bridge, Request request)
        {
            return request.RequestContext.Runtime.ExecuteNextAsync<Response>(
                bridge,
                request,
                request.RequestContext,
                true,
                PeripheralType.Opos);
        }
    }
}
