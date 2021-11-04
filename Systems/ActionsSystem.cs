using System.Collections.Generic;

using MyHome.Systems.Actions;
using MyHome.Utils;

namespace MyHome.Systems
{
    public class ActionsSystem : BaseSystem
    {
        [UiProperty(true)]
        public List<BaseAction> Actions { get; }


        private ActionsSystem() : this(null) { }  // for json deserialization

        public ActionsSystem(MyHome owner) : base(owner)
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
            // TODO: allow action execution per timer - turn on lights for 5 seconds
            // TODO: manually activated security alarm shouldn't be stopped
            // TODO: add sunset/sunrise (weather state) as time of execution
        }
    }
}
