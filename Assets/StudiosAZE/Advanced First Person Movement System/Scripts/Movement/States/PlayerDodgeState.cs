using UnityEngine;

namespace AZE.AdvancedFirstPerson
{
    public class PlayerDodgeState : PlayerBaseState
    {
        private float _timer;
        private Vector3 _dodgeDirection;

        public PlayerDodgeState(PlayerMovementStateMachine ctx, PlayerStateFactory factory) : base(ctx, factory) { }

        public override void EnterState()
        {
            if (!ctx.useDodge) return;

            _timer = 0f;
            ctx.LastDodgeTime = Time.time;
            ctx.TargetHeight = ctx.GetStandingHeight();

            Vector3 localDir = Vector3.zero;

            if (ctx.InputHandler.DodgeDirection.y < 0) localDir += Vector3.back;
            if (ctx.InputHandler.DodgeDirection.x < 0) localDir += Vector3.left;
            if (ctx.InputHandler.DodgeDirection.x > 0) localDir += Vector3.right;

            if (localDir == Vector3.zero)
            {
                localDir = Vector3.back;
            }

            _dodgeDirection = ctx.CameraTransform.TransformDirection(localDir);
            _dodgeDirection.y = 0;
            _dodgeDirection.Normalize();

            ctx.InputHandler.UseDodge();
        }

        public override void UpdateState()
        {
            ctx.HandleGravity();

            if (_timer < ctx.DodgeDuration)
            {
                float currentDodgeSpeed = Mathf.Lerp(ctx.DodgeSpeed, 0f, _timer / ctx.DodgeDuration);
                ctx.CurrentMoveVelocity = _dodgeDirection * currentDodgeSpeed;
                _timer += Time.deltaTime;
            }
            else
            {
                CheckSwitchStates();
            }
        }

        public override void CheckSwitchStates()
        {
            if (ctx.InputHandler.SprintPressed)
                ctx.SwitchState(factory.Sprint);
            else
                ctx.SwitchState(factory.Walk);
        }

        public override void ExitState()
        {
            ctx.CurrentMoveVelocity = Vector3.zero;
        }

        public override void InitializeSubState() { }
    }
}