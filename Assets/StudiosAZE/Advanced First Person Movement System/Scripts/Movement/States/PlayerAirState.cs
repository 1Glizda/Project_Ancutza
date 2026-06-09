namespace AZE.AdvancedFirstPerson
{
    public class PlayerAirState : PlayerBaseState
    {
        public PlayerAirState(PlayerMovementStateMachine ctx, PlayerStateFactory factory) : base(ctx, factory) { }

        public override void EnterState()
        {
            ctx.TargetHeight = ctx.GetStandingHeight();
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
            ctx.HandleGravity();
            ctx.HandleMovement(ctx.WalkSpeed);
        }

        public override void CheckSwitchStates()
        {
            if (ctx.JumpBufferCounter > 0 && ctx.CoyoteTimeCounter > 0)
            {
                ctx.SwitchState(factory.Jump);
                return;
            }

            if (ctx.IsGrounded)
            {
                if (ctx.InputHandler.MoveInput.magnitude < 0.1f)
                {
                    ctx.SwitchState(factory.Idle);
                }
                else
                {
                    if (ctx.InputHandler.SprintPressed)
                        ctx.SwitchState(factory.Sprint);
                    else
                        ctx.SwitchState(factory.Walk);
                }
            }
        }

        public override void ExitState() { }
        public override void InitializeSubState() { }
    }
}