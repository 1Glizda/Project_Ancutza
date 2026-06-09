namespace AZE.AdvancedFirstPerson
{
    public class PlayerSprintState : PlayerBaseState
    {
        public PlayerSprintState(PlayerMovementStateMachine ctx, PlayerStateFactory factory) : base(ctx, factory) { }

        public override void EnterState()
        {
            ctx.TargetHeight = ctx.GetStandingHeight();
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
            ctx.HandleGravity();
            ctx.HandleMovement(ctx.RunSpeed);
        }

        public override void ExitState() 
        {
        }

        public override void CheckSwitchStates()
        {
            if (!ctx.IsGrounded)
            {
                ctx.SwitchState(factory.Air);
            }

            if (ctx.JumpBufferCounter > 0)
            {
                ctx.SwitchState(factory.Jump);
                return;
            }

            if (ctx.InputHandler.JumpTriggered)
            {
                ctx.SwitchState(factory.Jump);
            }

            if (ctx.InputHandler.CrouchTriggered)
            {
                ctx.SwitchState(factory.Crouch);
            }

            if (ctx.InputHandler.DodgeTriggered && ctx.CanDodge())
            {
                ctx.SwitchState(factory.Dodge);
                return;
            }

            if (!ctx.InputHandler.SprintPressed && ctx.InputHandler.MoveInput.magnitude > 0.1f)
            {
                ctx.SwitchState(factory.Walk);
            }

            if (ctx.InputHandler.MoveInput.magnitude < 0.1f)
            {
                ctx.SwitchState(factory.Idle);
            }
        }

        public override void InitializeSubState() { }
    }
}