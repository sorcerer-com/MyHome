using System.Collections.Generic;

using MyHome.Systems.Actions;
using MyHome.Utils;

namespace MyHome.Systems
{
    public class ActionsSystem : BaseSystem
    {
        [UiProperty]
        public List<BaseAction> Actions { get; }


        public ActionsSystem()
        {
            this.Actions = new List<BaseAction>();
        }


        public override void Setup()
        {
            base.Setup();

            foreach (var action in this.Actions)
                action.Setup();
        }

        public override void Update()
        {
            base.Update();

            this.Actions.RunForEach(action => action.Update());
            // TODO: manually activated security alarm shouldn't be stopped
            // TODO: add sunset/sunrise (weather state) as time of execution
        }
    }
}
