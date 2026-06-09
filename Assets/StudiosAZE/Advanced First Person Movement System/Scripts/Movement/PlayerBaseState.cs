using UnityEngine;

namespace AZE.AdvancedFirstPerson
{
    public abstract class PlayerBaseState
    {
        protected PlayerMovementStateMachine ctx;
        protected PlayerStateFactory factory;

        public PlayerBaseState(PlayerMovementStateMachine currentContext, PlayerStateFactory playerStateFactory)
        {
            ctx = currentContext;
            factory = playerStateFactory;
        }

        public abstract void EnterState();
        public abstract void UpdateState();
        public abstract void ExitState();
        public abstract void CheckSwitchStates();
        public abstract void InitializeSubState();
    }
}