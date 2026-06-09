namespace AZE.AdvancedFirstPerson
{
    public class PlayerJumpState : PlayerBaseState
    {
        public PlayerJumpState(PlayerMovementStateMachine ctx, PlayerStateFactory factory) : base(ctx, factory) { }

        public override void EnterState()
        {
            if (!ctx.useJump) return;

            ctx.JumpBufferCounter = 0f;
            ctx.CoyoteTimeCounter = 0f;

            ctx.TargetHeight = ctx.GetStandingHeight();
            ctx.VerticalVelocity = ctx.JumpForce;
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
            ctx.HandleGravity();
            ctx.HandleMovement(ctx.WalkSpeed);
        }

        public override void CheckSwitchStates()
        {
            if (ctx.VerticalVelocity < 0)
            {
                ctx.SwitchState(factory.Air);
            }
        }

        public override void ExitState() { }
        public override void InitializeSubState() { }
    }
}