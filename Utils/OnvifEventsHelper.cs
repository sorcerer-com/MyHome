using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

using Mictlanix.DotNet.Onvif.Common;
using Mictlanix.DotNet.Onvif.Device;
using Mictlanix.DotNet.Onvif.Security;

using OnvifEvents;

namespace MyHome.Utils
{
    public static class OnvifEventsHelper
    {
        // based on https://github.com/pedoc/onvif/blob/master/src/OnvifClientFactory.cs
        // used as ref https://github.com/BogdanovKirill/OnvifEventsReceiver/tree/master
        private static Binding CreateBinding()
        {
            var binding = new CustomBinding();
            var textBindingElement = new TextMessageEncodingBindingElement
            {
                MessageVersion = MessageVersion.CreateVersion(EnvelopeVersion.Soap12, AddressingVersion.None)
            };
            var httpBindingElement = new HttpTransportBindingElement
            {
                AllowCookies = true,
                MaxBufferSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue
            };

            binding.Elements.Add(textBindingElement);
            binding.Elements.Add(httpBindingElement);

            return binding;
        }

        private static async Task<TimeSpan> GetDeviceTimeShift(DeviceClient device)
        {
            var utc = (await device.GetSystemDateAndTimeAsync()).UTCDateTime;
            var dt = new System.DateTime(utc.Date.Year, utc.Date.Month, utc.Date.Day,
                utc.Time.Hour, utc.Time.Minute, utc.Time.Second);
            return dt - System.DateTime.UtcNow;
        }

        public static async Task<PullPointSubscriptionClient> CreateEventsAsync(DeviceClient deviceClient, string username, string password)
        {
            var binding = CreateBinding();
            var caps = await deviceClient.GetCapabilitiesAsync(new CapabilityCategory[] { CapabilityCategory.Events });
            var events = new PullPointSubscriptionClient(binding, new EndpointAddress(new Uri(caps.Capabilities.Events.XAddr)));

            var time_shift = await GetDeviceTimeShift(deviceClient);
            events.ChannelFactory.Endpoint.EndpointBehaviors.Clear();
            events.ChannelFactory.Endpoint.EndpointBehaviors.Add(new SoapSecurityHeaderBehavior(username, password, time_shift));

            // Connectivity Test
            await events.OpenAsync();

            return events;
        }
    }
}
