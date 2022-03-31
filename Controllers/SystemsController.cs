using System;
using System.Linq;

using Microsoft.AspNetCore.Mvc;

using MyHome.Systems.Actions;
using MyHome.Systems.Actions.Conditions;
using MyHome.Systems.Actions.Executors;
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
        public ActionResult GetSystems()
        {
            return this.Ok(this.myHome.Systems.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToUiObject()));
        }

        [HttpGet("{systemName}")]
        public ActionResult GetSystem(string systemName)
        {
            var system = this.myHome.Systems.Values.FirstOrDefault(s => s.Name.ToLower() == systemName.ToLower());
            if (system == null)
                return this.NotFound($"System '{systemName}' not found");

            return this.Ok(system.ToUiObject());
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
                logger.Error($"Failed to set system '{systemName}'");
                logger.Debug(e);
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
                    var result = method.Invoke(system, args);
                    return this.Ok(result);
                }
                return this.NotFound("No such function: " + funcName);
            }
            catch (Exception e)
            {
                logger.Error($"Failed to call '{systemName}' system's function '{funcName}'");
                logger.Debug(e);
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
            return this.Ok(action.ToUiObject());
        }

        [HttpPost("Actions/Condition/create/{conditionType}")]
        public ActionResult CreateActionCondition(string conditionType)
        {
            var type = typeof(BaseCondition).GetSubClasses().FirstOrDefault(t => t.Name == conditionType);
            if (type == null)
                return this.NotFound("No such condition type: " + conditionType);

            var condition = (BaseCondition)Activator.CreateInstance(type, true);
            return this.Ok(condition.ToUiObject());
        }

        [HttpPost("Actions/Executor/create/{executorType}")]
        public ActionResult CreateActionExecutor(string executorType)
        {
            var type = typeof(BaseExecutor).GetSubClasses().FirstOrDefault(t => t.Name == executorType);
            if (type == null)
                return this.NotFound("No such executor type: " + executorType);

            var executor = (BaseExecutor)Activator.CreateInstance(type, true);
            return this.Ok(executor.ToUiObject());
        }

        [HttpPost("Actions/{actionName}")]
        public ActionResult SetAction(string actionName)
        {
            try
            {
                var action = this.myHome.ActionsSystem.Actions.FirstOrDefault(action => action.Name == actionName);
                if (action == null)
                {
                    logger.Info($"Add action: {actionName}");
                    var actionType = System.Reflection.Assembly.GetExecutingAssembly().GetType(this.Request.Form["$type"]);
                    if (actionType == null)
                        return this.NotFound("No such action type: " + actionType);

                    action = (BaseAction)Activator.CreateInstance(actionType, true);
                    action.Setup();
                    this.myHome.ActionsSystem.Actions.Add(action);
                }

                if (this.Request.Form.ContainsKey("ActionCondition[$type]"))
                {
                    var conditionType = System.Reflection.Assembly.GetExecutingAssembly().GetType(this.Request.Form["ActionCondition[$type]"]);
                    if (conditionType != null)
                    {
                        var condition = (BaseCondition)Activator.CreateInstance(conditionType, true);
                        action.ActionCondition = condition;
                    }
                    else
                        logger.Error($"No such condition type: {conditionType}");
                }
                else
                    action.ActionCondition = null;


                var executorType = System.Reflection.Assembly.GetExecutingAssembly().GetType(this.Request.Form["Executor[$type]"]);
                if (executorType != null)
                {
                    var executor = (BaseExecutor)Activator.CreateInstance(executorType, true);
                    action.Executor = executor;
                }
                else
                    logger.Error($"No such executor type: {executorType}");

                this.Request.Form.SetObject(action);
                this.myHome.SystemChanged = true;
                return this.Ok();
            }
            catch (Exception e)
            {
                logger.Error($"Failed to set action: {actionName}");
                logger.Debug(e);
                return this.BadRequest(e.Message);
            }
        }

        [HttpPost("Actions/{actionName}/delete")]
        public ActionResult DeleteAction(string actionName)
        {
            try
            {
                logger.Info($"Delete action: {actionName}");
                var action = this.myHome.ActionsSystem.Actions.FirstOrDefault(action => action.Name == actionName);
                if (action == null)
                    return this.NotFound($"Action '{actionName}' not found");

                this.myHome.ActionsSystem.Actions.Remove(action);
                this.myHome.SystemChanged = true;
                return this.Ok();
            }
            catch (Exception e)
            {
                logger.Error($"Failed to delete action: {actionName}");
                logger.Debug(e);
                return this.BadRequest(e.Message);
            }
        }
    }
}
