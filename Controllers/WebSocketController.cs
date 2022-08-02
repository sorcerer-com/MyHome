using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MyHome.Controllers
{
    [Route("/ws")]
    [ApiController]
    public class WebSocketController : ControllerBase
    {
        private readonly MyHome myHome;
        private bool shouldRefresh = false;


        public WebSocketController(MyHome myHome)
        {
            this.myHome = myHome;
            this.myHome.Events.Handler += (_, __) => this.shouldRefresh = true;
        }


        [HttpGet("refresh")]
        public async Task Refresh()
        {
            if (!this.HttpContext.WebSockets.IsWebSocketRequest)
            {
                this.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            try
            {
                using var webSocket = await this.HttpContext.WebSockets.AcceptWebSocketAsync();

                var buffer = new byte[1024 * 4];
                var receiveResult = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);

                // implement ping-pong effect to check the connectivity
                while (!receiveResult.CloseStatus.HasValue)
                {
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                        receiveResult.MessageType,
                        receiveResult.EndOfMessage,
                        CancellationToken.None);

                    if (this.shouldRefresh)
                    {
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(Encoding.Default.GetBytes("refresh")),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);
                        this.shouldRefresh = false;
                    }

                    receiveResult = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), CancellationToken.None);
                }

                await webSocket.CloseAsync(
                    receiveResult.CloseStatus.Value,
                    receiveResult.CloseStatusDescription,
                    CancellationToken.None);
            }
            catch
            {
                // nothing we can do
            }
        }
    }
}
