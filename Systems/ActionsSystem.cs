using System.Collections.Generic;

using MyHome.Systems.Actions;
using MyHome.Utils;

namespace MyHome.Systems
{
    public class ActionsSystem : BaseSystem
    {
        // TODO: consider do as list, not a dictionary, to allow renaming
        [UiProperty(true)]
        public Dictionary<string, BaseAction> Actions { get; } // name / action


        private ActionsSystem() : this(null) { }  // for json deserialization

        public ActionsSystem(MyHome owner) : base(owner)
        {
            this.Actions = new Dictionary<string, BaseAction>();
        }


        public override void Setup()
        {
            base.Setup();

            foreach (var action in this.Actions.Values)
                action.Setup();
        }

        public override void Update()
        {
            base.Update();

            this.Actions.Values.RunForEach(action => action.Update());
            // TODO: AND action - split trigger part from the action
        }
    }
}
