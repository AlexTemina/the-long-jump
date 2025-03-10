using Assets.Scripts.managers;
using UnityEngine;
using UnityEngine.Events;
using static Enum;

namespace Assets.Scripts.being
{
    public class CharacterMover : MonoBehaviour
    {
        [Tooltip("Amount of time that the player can still jump after leaving ground")]
        [SerializeField] private float _coyoteTime = .1f;

        [Tooltip("player speed")]
        [SerializeField] private float _runSpeed = 40f;

        [Tooltip("Circle that determines if we are grounded or not")]
        [SerializeField] private float _groundCheckRadius = 1f;

        [Tooltip("Amount of force added when the player jumps")]
        [SerializeField] private float _jumpForce = 600f;

        [Tooltip("Amount of time that the player has to wait before jumping again")]
        [SerializeField] private float _jumpTime = .1f;

        [Tooltip("A mask determining what is ground to the character")]
        [SerializeField] private LayerMask _whatIsGround = new();

        [Tooltip(" A position marking where to check if the player is grounded")]
        [SerializeField] private Transform _groundCheck;

        [SerializeField] private TrailRenderer _trail;

        [Tooltip("The Physics Material to use when we are in the air")]
        [SerializeField] private PhysicsMaterial2D _airPhysicsMaterial;

        [Tooltip("The Physics Material to use when we are in the ground")]
        [SerializeField] private PhysicsMaterial2D _groundPhysicsMaterial;

        [Tooltip("Gravity when falliong down")]
        [SerializeField] private float _downwardsGravityScale = 9f;

        [Tooltip("Gravity when jumping up")]
        [SerializeField] private float _upwardsGravityScale = 3f;

        [Tooltip("how much in units you have to fall to die")]
        [SerializeField] private float _fallDeath = 5f;

        [SerializeField] private RuntimeAnimatorController _berserkAnimator;

        [SerializeField] private GameObject _bodyParts;

        public float FallDeath { get => _fallDeath; set => _fallDeath = value; }
        public bool IsGrounded { get; private set; } = false;
        public StateMachine<CharState> state = new(CharState.Airing);
        public string currentStateName = CharState.Airing.ToString();

        private Animator _animatorComponent;
        private Rigidbody2D _body;
        private CharacterEffects _effects;
        private CharacterAnimator _charAnimator;
        private CharacterCommander _charCommander;
        private PlayerAudioSource _audioSource;
        private GroundHitAudioSource _groundHitSource;

        private float _originalGravityScale;
        private float _coyoteCounter;
        private float _jumpCounter;
        private float _fallDamageFirstPosition;
        private bool _isDead;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _effects = GetComponent<CharacterEffects>();
            _charAnimator = GetComponent<CharacterAnimator>();
            _charCommander = GetComponent<CharacterCommander>();
            _audioSource = GetComponent<PlayerAudioSource>();
            _animatorComponent = GetComponent<Animator>();
            _groundHitSource = _groundCheck.GetComponent<GroundHitAudioSource>();

            _originalGravityScale = _body.gravityScale;

            _coyoteCounter = _coyoteTime;
            _jumpCounter = _jumpTime;
            _fallDamageFirstPosition = transform.position.y;

            _body.sharedMaterial = _airPhysicsMaterial;

            state.StateChanged.AddListener(StateChanged);
        }

        private void FixedUpdate()
        {
            currentStateName = state.CurrentState.ToString();

            Collider2D[] colliders = Physics2D.OverlapCircleAll(_groundCheck.transform.position, _groundCheckRadius, _whatIsGround);
            bool isGrounded = colliders.Length > 0;

            switch (state.CurrentState)
            {
                case CharState.Airing:
                    WhileAiring(isGrounded);
                    break;

                case CharState.Coyoting:
                    WhileCoyoting(isGrounded);
                    break;

                case CharState.Grounded:
                    WhileGrounded(isGrounded);
                    break;

                case CharState.Impulsing:
                    WhileImpulsing(isGrounded);
                    break;

                case CharState.Jumping:
                    WhileJumping(isGrounded);
                    break;

                default:
                    break;
            }
        }

        private void WhileAiring(bool isGrounded)
        {
            if (isGrounded)
            {
                state.ChangeState(CharState.Grounded);
            }
            else
            {
                _body.gravityScale = _body.velocity.y > 0 ? _upwardsGravityScale : _downwardsGravityScale;
                _fallDamageFirstPosition = Mathf.Max(transform.position.y, _fallDamageFirstPosition);
            }
        }

        private void WhileCoyoting(bool isGrounded)
        {
            if (isGrounded)
            {
                state.ChangeState(CharState.Grounded);
            }
            else
            {

                _coyoteCounter -= Time.fixedDeltaTime;

                if (_coyoteCounter <= 0f)
                {
                    state.ChangeState(CharState.Airing);
                }
            }
        }

        private void WhileGrounded(bool isGrounded)
        {

            if (!isGrounded)
            {
                state.ChangeState(CharState.Coyoting);
            }
            else
            {
                _effects.ActivateRun(_body.velocity.x);
            }
        }

        private void WhileImpulsing(bool isGrounded)
        {
            if (isGrounded)
            {
                state.ChangeState(CharState.Grounded);
            }
            else
            {
                _fallDamageFirstPosition = Mathf.Max(transform.position.y, _fallDamageFirstPosition);
            }
        }

        private void WhileJumping(bool isGrounded)
        {
            _jumpCounter -= Time.deltaTime;

            if (_jumpCounter <= 0)
            {
                if (isGrounded)
                {
                    state.ChangeState(CharState.Grounded);
                }
            }

            if (!isGrounded)
            {
                _body.gravityScale = _body.velocity.y > 0 ? _upwardsGravityScale : _downwardsGravityScale;
                _fallDamageFirstPosition = Mathf.Max(transform.position.y, _fallDamageFirstPosition);
            }
        }

        private void StateChanged(CharState newState, CharState previousState)
        {
            if (previousState == CharState.Coyoting)
            {
                OnStoppedCoyoting();
            }

            switch (previousState)
            {
                case CharState.Grounded:
                    OnStoppedGrounded();
                    break;

                case CharState.Coyoting:
                    OnStoppedCoyoting();
                    break;

                case CharState.Airing:
                    break;

                case CharState.Jumping:
                    OnStoppedJumping();
                    break;

                case CharState.Impulsing:
                    break;
                default:
                    break;
            }

            switch (newState)
            {
                case CharState.Airing:
                    OnAir();
                    break;

                case CharState.Coyoting:
                    OnCoyote();
                    break;

                case CharState.Grounded:
                    OnGround();
                    break;

                case CharState.Impulsing:
                    OnImpulse();
                    break;

                case CharState.Jumping:
                    OnJump();
                    break;

                default:
                    break;
            }
        }

        private void OnStoppedGrounded()
        {
            _effects.ActivateRun(0);
        }

        private void OnStoppedCoyoting()
        {
            _body.gravityScale = _originalGravityScale;
            _coyoteCounter = _coyoteTime;
        }

        private void OnStoppedJumping()
        {
            _jumpCounter = _jumpTime;
        }

        private void OnAir()
        {
            _body.sharedMaterial = _airPhysicsMaterial;
            _fallDamageFirstPosition = transform.position.y;
        }

        private void OnCoyote()
        {
            _body.velocity = new(_body.velocity.x, 0f);
            _originalGravityScale = _body.gravityScale;
            _body.gravityScale = 0;
        }

        private void OnGround()
        {
            _body.gravityScale = _downwardsGravityScale;

            _body.sharedMaterial = _groundPhysicsMaterial;
            float fallDistance = _fallDamageFirstPosition - transform.position.y;
            if (fallDistance >= _fallDeath)
            {
                _audioSource.PlaySound(PlayerSounds.Death);
                Kill(DeathType.Fall);
            }

            _effects.BurstFall();
            _audioSource.PlaySound(PlayerSounds.Grounded);
            _groundHitSource.PlaySound();
        }

        private void OnImpulse()
        {
            _body.gravityScale = _downwardsGravityScale;

            _body.sharedMaterial = _airPhysicsMaterial;
            _fallDamageFirstPosition = transform.position.y;

            _charAnimator.TriggerJump();
        }

        private void OnJump()
        {
            _body.sharedMaterial = _airPhysicsMaterial;
            _fallDamageFirstPosition = transform.position.y;

            _effects.BurstJump();
            _charAnimator.TriggerJump();
            _audioSource.PlaySound(PlayerSounds.Jump);
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(_groundCheck.position, _groundCheckRadius);
        }

        public void Move(float move)
        {
            if (!CanMove())
            {
                return;
            }

            float speed = move * 10f * _runSpeed;

            _body.velocity = new Vector2(speed, _body.velocity.y);
        }

        public bool CanJump()
        {
            return !_isDead && state.IsInState(CharState.Grounded, CharState.Coyoting);
        }

        public bool CanMove()
        {
            return !_isDead && !state.IsInState(CharState.Impulsing);
        }

        public void Jump(float force = 1, Vector2? direction = null)
        {
            if (!CanJump())
            {
                return;
            }

            if (direction == null)
            {
                direction = Vector2.up;
            }

            _body.AddForce(_jumpForce * force * (Vector2)direction, ForceMode2D.Force);

            state.ChangeState(CharState.Jumping);
        }

        public void Impulse(float force, Vector2 direction)
        {
            _body.velocity = Vector2.zero;

            _body.AddForce(force * direction, ForceMode2D.Force);

            state.ChangeState(CharState.Impulsing);
        }

        public void Kill(DeathType type, float deathTime = 1f)
        {
            if (_isDead)
            {
                return;
            }

            _trail.emitting = false;

            _body.velocity = Vector2.zero;

            _isDead = true;

            int animationName = -1;
            UnityAction burst = null;
            switch (type)
            {
                case DeathType.Fall:
                    animationName = CharAnimationNames.Death;
                    break;
                case DeathType.Abism:
                    _audioSource.PlaySound(PlayerSounds.Scream);
                    break;
                case DeathType.Drown:
                    burst = _effects.BurstDrown;
                    _audioSource.PlaySound(PlayerSounds.Drown);
                    break;
                case DeathType.Reset:
                    break;
                case DeathType.Spikes:
                    _ = Instantiate(_bodyParts, transform.position, Quaternion.identity);
                    break;
                default:
                    break;
            }

            if (animationName != -1)
            {
                _charAnimator.TriggerDeath(animationName);
            }

            burst?.Invoke();

            AddTeleportCommand(transform.position, null, animationName != -1, deathTime);
            print(CheckpointManager.Instance.ActiveCheckpoint.SpawnPoint);
            AddTeleportCommand(CheckpointManager.Instance.ActiveCheckpoint.SpawnPoint, () => _isDead = false);
        }

        public void AddTeleportCommand(Vector3 position, UnityAction onTeleported = null, bool showWhileTeleporting = false, float time = 0.5f)
        {
            CommanderCase commanderCase = new()
            {
                Target = position,
                OnEnd = () =>
                {
                    _fallDamageFirstPosition = transform.position.y;
                    onTeleported?.Invoke();
                },
                ShouldShow = showWhileTeleporting,
                Time = time,
            };

            _charCommander.AddCommand(commanderCase);
        }

        public void EnterTeleport()
        {
            _charAnimator.EnterTeleport();
        }

        public void ExitTeleport()
        {
            _charAnimator.ExitTeleport();
        }

        public void SwitchTrailEmission(bool shouldEmit)
        {
            if (!_isDead)
            {
                _trail.emitting = shouldEmit;
            }
        }

        public void GoBerserk()
        {
            _animatorComponent.runtimeAnimatorController = _berserkAnimator;
        }
    }
}

