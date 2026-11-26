using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.HardwareStation.CardPayment;
using Microsoft.Dynamics.Commerce.HardwareStation.PeripheralRequests;
using Microsoft.Dynamics.Commerce.HardwareStation.Peripherals;
using Microsoft.Dynamics.Commerce.Runtime.Handlers;
using Microsoft.Dynamics.Commerce.Runtime.Messages;
using Newtonsoft.Json.Linq;

namespace IIGCore.HardwareStation.PosTerminalWeb.Handlers
{
    /// <summary>
    /// Payment terminal → PosTerminal.Web PaymentTerminal/* routes.
    /// HandlerName matches the hardware-profile payment device name (payment convention).
    /// </summary>
    public sealed class PtwPaymentTerminalDevice : INamedRequestHandlerAsync
    {
        private string _deviceName = PosTerminalWebConstants.PaymentTerminal;
        private string _token;

        /// <summary>
        /// Payment devices resolve by device name, not PeripheralType.
        /// </summary>
        public string HandlerName => PosTerminalWebConstants.PaymentTerminal;

        public IEnumerable<Type> SupportedRequestTypes => new[]
        {
            typeof(LockPaymentTerminalDeviceRequest),
            typeof(ReleasePaymentTerminalDeviceRequest),
            typeof(OpenPaymentTerminalDeviceRequest),
            typeof(ClosePaymentTerminalDeviceRequest),
            typeof(BeginTransactionPaymentTerminalDeviceRequest),
            typeof(EndTransactionPaymentTerminalDeviceRequest),
            typeof(CancelOperationPaymentTerminalDeviceRequest),
            typeof(UpdateLineItemsPaymentTerminalDeviceRequest),
            typeof(AuthorizePaymentTerminalDeviceRequest),
            typeof(CapturePaymentTerminalDeviceRequest),
            typeof(VoidPaymentTerminalDeviceRequest),
            typeof(RefundPaymentTerminalDeviceRequest),
        };

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));
            var client = PosTerminalWebStationClient.Shared;

            switch (request)
            {
                case LockPaymentTerminalDeviceRequest lockReq:
                    _deviceName = PtwDeviceName.From(lockReq, _deviceName) ?? PosTerminalWebConstants.PaymentTerminal;
                    _token = await client.PostForTokenAsync(
                        "PaymentTerminal/Lock",
                        PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return new LockPaymentTerminalDeviceResponse(_token);

                case ReleasePaymentTerminalDeviceRequest _:
                case ClosePaymentTerminalDeviceRequest _:
                    await client.PostAsync(
                        "PaymentTerminal/Unlock",
                        new { deviceName = _deviceName, token = _token }).ConfigureAwait(false);
                    _token = null;
                    return NullResponse.Instance;

                case OpenPaymentTerminalDeviceRequest _:
                case BeginTransactionPaymentTerminalDeviceRequest _:
                case EndTransactionPaymentTerminalDeviceRequest _:
                case CancelOperationPaymentTerminalDeviceRequest _:
                    return NullResponse.Instance;

                case UpdateLineItemsPaymentTerminalDeviceRequest update:
                    await client.PostAsync(
                        "PaymentTerminal/UpdateLineItems",
                        new
                        {
                            deviceName = _deviceName,
                            token = update.Token ?? _token,
                            totalAmount = update.TotalAmount,
                            taxAmount = update.TaxAmount,
                            discountAmount = update.DiscountAmount,
                            subTotalAmount = update.SubTotalAmount,
                            lines = update.Items,
                        }).ConfigureAwait(false);
                    return NullResponse.Instance;

                case AuthorizePaymentTerminalDeviceRequest authorize:
                    return await PaymentAsync<AuthorizePaymentTerminalDeviceResponse>(
                        client,
                        "PaymentTerminal/AuthorizePayment",
                        authorize.Token,
                        authorize.Amount,
                        authorize.Currency).ConfigureAwait(false);

                case CapturePaymentTerminalDeviceRequest capture:
                    return await PaymentAsync<CapturePaymentTerminalDeviceResponse>(
                        client,
                        "PaymentTerminal/CapturePayment",
                        capture.Token,
                        capture.Amount,
                        capture.Currency).ConfigureAwait(false);

                case RefundPaymentTerminalDeviceRequest refund:
                    return await PaymentAsync<RefundPaymentTerminalDeviceResponse>(
                        client,
                        "PaymentTerminal/RefundPayment",
                        refund.Token,
                        refund.Amount,
                        refund.Currency).ConfigureAwait(false);

                case VoidPaymentTerminalDeviceRequest voidReq:
                    return await PaymentAsync<VoidPaymentTerminalDeviceResponse>(
                        client,
                        "PaymentTerminal/VoidPayment",
                        voidReq.Token,
                        voidReq.Amount,
                        voidReq.Currency).ConfigureAwait(false);

                default:
                    throw new NotSupportedException($"Request '{request.GetType().FullName}' is not supported by {HandlerName}.");
            }
        }

        private async Task<TResponse> PaymentAsync<TResponse>(
            PosTerminalWebStationClient client,
            string route,
            string token,
            decimal? amount,
            string currency)
            where TResponse : Response
        {
            var json = await client.PostAsync<JToken>(
                route,
                new
                {
                    deviceName = _deviceName,
                    token = token ?? _token,
                    amount,
                    currency,
                }).ConfigureAwait(false);

            var approved = json?["isApproved"]?.Value<bool?>()
                ?? json?["IsApproved"]?.Value<bool?>()
                ?? true;
            var paymentInfo = new PaymentInfo
            {
                IsApproved = approved,
                ApprovedAmount = amount ?? 0m,
                AvailableBalanceAmount = amount ?? 0m,
                CardNumberMasked = json?["cardNumberMasked"]?.ToString() ?? json?["CardNumberMasked"]?.ToString(),
                SignatureData = json?["signatureData"]?.ToString() ?? json?["SignatureData"]?.ToString(),
                PaymentSdkData = json?["paymentSdkData"]?.ToString() ?? json?["PaymentSdkData"]?.ToString() ?? route,
                CardType = CardType.InternationalCreditCard,
            };

            return (TResponse)Activator.CreateInstance(typeof(TResponse), paymentInfo);
        }
    }
}
