using System.Linq;

using Microsoft.AspNetCore.Mvc;

using MyHome.Utils;

namespace MyHome
{

    [Route("[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly MyHome myHome;

        public ApiController(MyHome myHome)
        {
            this.myHome = myHome;
        }

        [HttpGet("rooms")]
        public ActionResult GetRooms()
        {
            return this.Ok(this.myHome.Rooms.Select(r => r.ToUiObject()));
        }
    }
}
