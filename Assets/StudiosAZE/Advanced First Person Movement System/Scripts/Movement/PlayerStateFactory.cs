namespace AZE.AdvancedFirstPerson
{
    public class PlayerStateFactory
    {
        PlayerMovementStateMachine _context;

        public PlayerBaseState Idle { get; private set; }
        public PlayerBaseState Walk { get; private set; }
        public PlayerBaseState Sprint { get; private set; }
        public PlayerBaseState Crouch { get; private set; }
        public PlayerBaseState Dodge { get; private set; }
        public PlayerBaseState Jump { get; private set; }
        public PlayerBaseState Air { get; private set; }

        public PlayerStateFactory(PlayerMovementStateMachine currentContext)
        {
            _context = currentContext;
            Idle = new PlayerIdleState(_context, this);
            Walk = new PlayerWalkState(_context, this);
            Sprint = new PlayerSprintState(_context, this);
            Crouch = new PlayerCrouchState(_context, this);
            Dodge = new PlayerDodgeState(_context, this);
            Jump = new PlayerJumpState(_context, this);
            Air = new PlayerAirState(_context, this);
        }
    }
}