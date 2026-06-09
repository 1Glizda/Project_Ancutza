using UnityEngine;

namespace AZE.AdvancedFirstPerson
{
    public class PlayerCrouchState : PlayerBaseState
    {
        public PlayerCrouchState(PlayerMovementStateMachine ctx, PlayerStateFactory factory) : base(ctx, factory) { }

        public override void EnterState()
        {
            if (!ctx.useCrouch) return;
            ctx.TargetHeight = ctx.GetCrouchHeight();
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
            ctx.HandleGravity();

            ctx.HandleMovement(ctx.CrouchSpeed);
        }

        public override void ExitState() { }

        public override void CheckSwitchStates()
        {
            if (!ctx.InputHandler.CrouchTriggered)
            {
                if (ctx.CanStandUp() && ctx.InputHandler.MoveInput.magnitude < 0.1f)
                {
                    ctx.SwitchState(factory.Idle);
                }

                if (ctx.CanStandUp() && ctx.InputHandler.MoveInput.magnitude > 0.1f)
                {
                    ctx.SwitchState(factory.Walk);
                }
            }
        }
        public override void InitializeSubState() { }
    }
}