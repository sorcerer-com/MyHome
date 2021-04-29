using System;
using System.Linq;

using Microsoft.AspNetCore.Mvc;

using MyHome.Systems.Actions;
using MyHome.Utils;

using NLog;

namespace MyHome.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SystemsController : ControllerBase
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private readonly MyHome myHome;


        public SystemsController(MyHome myHome)
        {
            this.myHome = myHome;
        }


        [HttpGet]
        public ActionResult GetSystems(bool settings)
        {
            return this.Ok(this.myHome.Systems.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToUiObject(settings)));
        }

        [HttpGet("{systemName}")]
        public ActionResult GetSystem(string systemName, bool settings)
        {
            var system = this.myHome.Systems.Values.FirstOrDefault(s => s.Name.ToLower() == systemName.ToLower());
            if (system == null)
                return this.NotFound($"System '{systemName}' not found");

            return this.Ok(system.ToUiObject(settings));
        }

        [HttpPost("{systemName}")]
        public ActionResult SetSystem(string systemName)
        {
            try
            {
                var system = this.myHome.Systems.FirstOrDefault(kvp => kvp.Key.ToLower() == systemName.ToLower()).Value;
                if (system == null)
                    return this.NotFound($"System '{systemName}' not found");

                this.Request.Form.SetObject(system);
                this.myHome.SystemChanged = true;
                return this.Ok();
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to set system '{systemName}'");
                return this.BadRequest(e.Message);
            }
        }

        [HttpPost("{systemName}/{funcName}")]
        public ActionResult CallSystem(string systemName, string funcName)
        {
            try
            {
                var system = this.myHome.Systems.Values.FirstOrDefault(s => s.Name.ToLower() == systemName.ToLower());
                if (system == null)
                    return this.NotFound($"System '{systemName}' not found");

                var method = system.GetType().GetMethod(funcName);
                if (method != null)
                {
                    var methodParams = method.GetParameters();
                    var args = Array.Empty<object>();
                    if (this.Request.HasFormContentType)
                    {
                        args = this.Request.Form.Select((kvp, i) =>
                            Utils.Utils.ParseValue(kvp.Value.ToString(), methodParams[i].ParameterType)).ToArray();
                    }
                    var result = method.Invoke(system, args.ToArray());
                    return this.Ok(result);
                }
                return this.NotFound("No such function: " + funcName);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to call '{systemName}' system's function '${funcName}'");
                return this.BadRequest(e.Message);
            }
        }


        [HttpPost("Actions/create/{actionType}")]
        public ActionResult CreateAction(string actionType)
        {
            var type = typeof(BaseAction).GetSubClasses().FirstOrDefault(t => t.Name == actionType);
            if (type == null)
                return this.NotFound("No such action type: " + actionType);

            var action = (BaseAction)Activator.CreateInstance(type, true);
            action.Owner = this.myHome.ActionsSystem;
            return this.Ok(action.ToUiObject(true));
        }

        [HttpPost("Actions/{actionName}")]
        public ActionResult SetAction(string actionName)
        {
            try
            {
                var action = this.myHome.ActionsSystem.Actions.FirstOrDefault(kvp => kvp.Key == actionName).Value;
                if (action == null)
                {
                    logger.Info("Add action: " + actionName);
                    var type = System.Reflection.Assembly.GetExecutingAssembly().GetType(this.Request.Form["$type"]);
                    if (type == null)
                        return this.NotFound("No such action type: " + type);

                    action = (BaseAction)Activator.CreateInstance(type, true);
                    action.Owner = this.myHome.ActionsSystem;
                    action.Setup();
                    this.myHome.ActionsSystem.Actions.Add(actionName, action);
                }

                this.Request.Form.SetObject(action);
                this.myHome.SystemChanged = true;
                return this.Ok();
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to set action: " + actionName);
                return this.BadRequest(e.Message);
            }
        }

        [HttpPost("Actions/{actionName}/delete")]
        public ActionResult DeleteAction(string actionName)
        {
            try
            {
                if (!this.myHome.ActionsSystem.Actions.ContainsKey(actionName))
                    return this.NotFound($"Action '{actionName}' not found");

                this.myHome.ActionsSystem.Actions.Remove(actionName);
                this.myHome.SystemChanged = true;
                return this.Ok();
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to delete action: " + actionName);
                return this.BadRequest(e.Message);
            }
        }
    }
}
