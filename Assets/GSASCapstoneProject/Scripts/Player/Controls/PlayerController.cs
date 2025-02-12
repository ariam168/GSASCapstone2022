///
/// Created by Dennis Chen
/// Reference: Tarodev's code talked about in https://www.youtube.com/watch?v=3sWTzMsmdx8
///

using Core.GlobalEvents;
using Core.GlobalVariables;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    public enum MeleeDashType
    {
        DodgeDash,
        OmniDir,
        AttackDash,
    }

    public enum RangedDashType
    {
        OmniDir,
        Charge,
        Penalty,
        Vertical
    }

    [SerializeField] private ScriptableStats _stats;
    [SerializeField] private PhysicsMaterial2D _movingMaterial;
    [SerializeField] private PhysicsMaterial2D _stationaryMaterial;
    [SerializeField] private MeleeDashType _meleeDashType;
    [SerializeField] private RangedDashType _rangedDashType;
    [SerializeField] private LayerMask _bossLayer;
    [SerializeField] private GlobalEvent _swapCompleted;
    [SerializeField] private GlobalEvent _swapCanceled;
    [SerializeField] private GlobalEvent _onDownAttackInAir;
    [SerializeField] private TransformGlobalEvent _requestSwap;
    [SerializeField] private TransformGlobalEvent _receiveSwapRequest;
    [SerializeField] private TransformVariable _playerTransform;
    [SerializeField] private BoolVariable _isKnockedBack;
    [SerializeField] private BoolVariable _isInvincible;
    [SerializeField] private BoolVariable _isCharging;
    [SerializeField] private FloatVariable _swapDelay;
    [SerializeField] private FloatVariable _movementCooldown;
    [SerializeField] private IntVariable _numDashes;


    #region Internal

    private Rigidbody2D _rb;
    private PlayerCombat _playerCombat;
    private BoxCollider2D _col; // Current collider
    private bool _cachedTriggerSetting;

    private Vector2 _moveDirection;
    private Vector2 _speed;
    private Vector2 _currentExternalVelocity;
    private Transform _otherPlayer;
    private int _fixedFrame;
    private bool _grounded;

    private InputActionAsset _actions;
    private InputAction _move, _jump, _dash, _attack, _swap;

    #endregion

    #region External
    public ScriptableStats PlayerStats => _stats;
    public Vector2 Input => _moveDirection;
    public Vector2 Speed => _speed;
    public Vector2 GroundNormal => _groundNormal;

    [HideInInspector]
    public bool IsShootingLaser;

    #endregion

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<BoxCollider2D>();
        _playerCombat = GetComponent<PlayerCombat>();
        _actions = GetComponentInParent<PlayerInput>().actions;

        _playerTransform.Value = transform;
        _cachedTriggerSetting = Physics2D.queriesHitTriggers;

        _move = _actions.FindActionMap("Player").FindAction("Movement");
        _jump = _actions.FindActionMap("Player").FindAction("Jump");
        _dash = _actions.FindActionMap("Player").FindAction("Movement Ability");
        _attack = _actions.FindActionMap("Player").FindAction("Attack");
        _swap = _actions.FindActionMap("Player").FindAction("Swap");

        _attack.started += ctx => HandleAttacking();
        _attack.canceled += ctx => CancelCharging();
        _jump.started += ctx => HandleJump();
        _jump.canceled += ctx => CancelJump();
        _dash.performed += ctx => StartDash();
        _swap.started += ctx => RequestSwap();
        _swap.canceled += ctx => CancelSwap();

        _numDashes.Value = _stats.MaxDashes;
        _movementCooldown.Value = _stats.MovementCooldown;
    }

    protected virtual void FixedUpdate()
    {
        _fixedFrame++;
        _currentExternalVelocity = Vector2.MoveTowards(_currentExternalVelocity, Vector2.zero, _stats.ExternalVelocityDecay * Time.fixedDeltaTime);
        if (_move.ReadValue<Vector2>().magnitude > 0.1f)
        {
            _moveDirection = _move.ReadValue<Vector2>().normalized;
            _moveDirection = AngleToDirection(Vector2.Angle(Vector2.right, _moveDirection), _moveDirection.y > 0, _moveDirection.x > 0);
        }
        else 
        {
            _moveDirection = Vector2.zero;
        }
        CheckCollisions();

        HandleCollisions();
        HandleHorizontal();
        HandleDash();
        HandleFall();

        ApplyVelocity();
    }

    private static Vector2 AngleToDirection(float angle, bool up, bool right)
    {
        int vertFactor = up ? 1 : -1;
        int horFactor = right ? 1 : -1;
        if (angle >= 22.5f && angle < 67.5f)
        {
            return new Vector2(1, 1 * vertFactor);
        }
        else if (angle >= 67.5f && angle < 112.5)
        {
            return new Vector2(0, 1 * vertFactor);
        }
        else if (angle >= 112.5 && angle < 157.5)
        {
            return new Vector2(-1, 1 * vertFactor);
        }
        else
        {
            return new Vector2(1 * horFactor, 0);
        }
    }

    #region Collisions

    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[2];
    private int _groundHitCount;
    private int _frameLeftGrounded = int.MinValue;

    protected virtual void CheckCollisions()
    {
        Physics2D.queriesHitTriggers = false;
        // Ground
        Vector2 origin = (Vector2)transform.position + _col.offset * transform.localScale;
        Vector2 _absScale = new Vector2(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y));
        _groundHitCount = Physics2D.BoxCastNonAlloc(origin, _col.size * _absScale, 0, Vector2.down, _groundHits, _stats.GrounderDistance, ~_stats.PlayerLayer);

        Physics2D.queriesHitTriggers = _cachedTriggerSetting;
    }

    protected virtual void HandleCollisions()
    {
        // Landed on the Ground
        if (!_grounded && _groundHitCount > 0)
        {
            _grounded = true;
            _canDash = true;
            _isKnockedBack.Value = false;
            ResetJump();
        }
        // Left the Ground
        else if (_grounded && _groundHitCount == 0)
        {
            _grounded = false;
            _frameLeftGrounded = _fixedFrame;
        }
    }

    #endregion

    #region Swapping
    private bool _swapHeld;

    protected virtual void RequestSwap()
    {
        _swapHeld = true;
        _requestSwap.Raise(transform);
    }

    protected virtual void CancelSwap()
    {
        _swapHeld = false;
        _swapCanceled.Raise();
    }

    protected virtual void ReceiveSwapRequest(Transform otherPlayer)
    {
        if (_swapHeld)
        {
            _otherPlayer = otherPlayer;

            gameObject.SetActive(false);
            otherPlayer.gameObject.SetActive(false);
            Invoke("Swap", _swapDelay.Value);
        }
    }

    private void Swap()
    {
        Vector3 tempPos = transform.position;
        transform.position = _otherPlayer.position;
        _otherPlayer.position = tempPos;
        gameObject.SetActive(true);
        _otherPlayer.gameObject.SetActive(true);
        _swapCompleted.Raise();
    }

    protected virtual void SwapCompleted()
    {
        _swapHeld = false;
    }

    #endregion

    #region Attacking


    protected virtual void HandleAttacking()
    {
        if (!_isKnockedBack.Value)
        {
            if(Mathf.Abs(_moveDirection.y) > 0 && _moveDirection.x == 0)
            {
                _playerCombat.VerticalAttack(_moveDirection.y > 0);
            }
            else
            {
                _playerCombat.Attack();
            }
        }
    }

    protected virtual void CancelCharging()
    {
        _playerCombat.CancelAttack();
    }

    protected virtual void DownAttackAirBounce()
    {
        _speed.y = _stats.DownAttackBounceSpeed;
    }

    #endregion

    #region Horizontal

    protected virtual void HandleHorizontal()
    {
        if (_isKnockedBack.Value) return;
        if (_moveDirection.x != 0 && !IsShootingLaser)
        {
            float inputX = _moveDirection.x;
            if((inputX > 0 && transform.localScale.x < 0) || (inputX < 0 && transform.localScale.x > 0))
            {
                transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
            }
            _speed.x = Mathf.MoveTowards(_speed.x, inputX * _stats.MaxSpeed, _stats.Acceleration * Time.fixedDeltaTime);
            _rb.sharedMaterial = _movingMaterial;
        }
        else
        {
            _speed.x = Mathf.MoveTowards(_speed.x, 0, (_grounded ? _stats.GroundDeceleration : _stats.AirDeceleration) * Time.fixedDeltaTime);
            if(_speed.x == 0) _rb.sharedMaterial = _stationaryMaterial;
        }
    }

    #endregion

    #region Jump

    private bool _endedJumpEarly;
    private bool _coyoteUsable;
    private bool _doubleJumpUsable;
    private bool _bufferedJumpUsable;
    private int _frameJumpWasPressed = int.MinValue;

    private bool CanUseCoyote => _coyoteUsable && !_grounded && _fixedFrame < _frameLeftGrounded + _stats.CoyoteFrames;
    private bool HasBufferedJump => _grounded && _bufferedJumpUsable && _fixedFrame < _frameJumpWasPressed + _stats.JumpBufferFrames;
    private bool CanDoubleJump => _stats.AllowDoubleJump && _doubleJumpUsable && !_coyoteUsable;

    protected virtual void HandleJump()
    {
        if (_isKnockedBack.Value || _dashing || IsShootingLaser) return;

        _frameJumpWasPressed = _fixedFrame;

        // Double jump
        /*if (CanDoubleJump)
        {
            _speed.y = _stats.JumpPower;
            _doubleJumpUsable = false;
            _endedJumpEarly = false;
        }*/

        // Standard jump
        if (CanUseCoyote || HasBufferedJump)
        {
            _coyoteUsable = false;
            _bufferedJumpUsable = false;
            _speed.y = _stats.JumpPower;
        }
    }

    protected virtual void CancelJump()
    {
        // Early end detection
        if (!_endedJumpEarly && !_grounded && !_dashing && _rb.velocity.y > 0) _endedJumpEarly = true;
    }


    protected virtual void ResetJump()
    {
        _coyoteUsable = true;
        _doubleJumpUsable = true;
        _bufferedJumpUsable = true;
        _endedJumpEarly = false;
    }

    #endregion

    #region Dash

    private bool _canDash;
    private bool _dashing;
    private int _startedDashing;
    private Vector2 _dashVel;

    protected virtual void StartDash()
    {
        if (_canDash && _numDashes.Value > 0 && !_isKnockedBack.Value && !IsShootingLaser)
        {
            _dashing = true;
            _canDash = false;
            _numDashes.Value--;
            _startedDashing = _fixedFrame;
            _dashVel = _moveDirection;

            // Strip external buildup
            _currentExternalVelocity = Vector2.zero;

            _dealtDamageThisDash = false;
        }
    }

    protected virtual void HandleDash()
    {
        if(_numDashes.Value < _stats.MaxDashes)
        {
            if (_movementCooldown.Value > 0)
            {
                _movementCooldown.Value -= Time.fixedDeltaTime;
            }
            else
            {
                _numDashes.Value++;
                _movementCooldown.Value = _stats.MovementCooldown;
            }
        }
        if (_stats.Melee)
        {
            switch (_meleeDashType)
            {
                case MeleeDashType.DodgeDash:
                    MeleeDodgeDash();
                    break;
                case MeleeDashType.OmniDir:
                    MeleeOmniDash();
                    break;
                case MeleeDashType.AttackDash:
                    MeleeAttackDash();
                    break;
            }
        }
        else
        {
            switch (_rangedDashType)
            {
                case RangedDashType.OmniDir:
                    RangedOmniTeleport();
                    break;
                case RangedDashType.Charge:
                    RangedChargeDash(_stats.ChargeGivenDash);
                    break;
                case RangedDashType.Penalty:
                    RangedChargeDash(_stats.PenaltyGivenDash);
                    break;
                case RangedDashType.Vertical:
                    RangedVerticalTeleport();
                    break;
            }
        }
    }

    private void MeleeDodgeDash()
    {
        _stats.DashDurationFrames = 9;
        if (_dashing)
        {
            _isInvincible.Value = true;
            _speed = new Vector2(transform.localScale.x > 0 ? 1 : -1, 0) * _stats.DashVelocity;
            // Cancel when the time is out or we've reached our max safety distance
            if(_fixedFrame > _startedDashing + _stats.DashInvincibleFrames)
            {
                _isInvincible.Value = false;
            }
            if (_fixedFrame > _startedDashing + _stats.DashDurationFrames)
            {
                _dashing = false;
                _speed.x *= _stats.DashEndHorizontalMultiplier;
                _canDash = true;
            }
        }
    }

    private void MeleeOmniDash()
    {
        _stats.DashDurationFrames = 7;
        if (_dashing)
        {
            _isInvincible.Value = true;
            if (_dashVel == Vector2.zero)
            {
                _speed = new Vector2(transform.localScale.x > 0 ? 1 : -1, 0) * _stats.DashVelocity;
            }
            else
            {
                _speed = new Vector2(_dashVel.x, _dashVel.y).normalized * _stats.DashVelocity;
            }
            if (_fixedFrame > _startedDashing + _stats.DashInvincibleFrames)
            {
                _isInvincible.Value = false;
            }
            // Cancel when the time is out or we've reached our max safety distance
            if (_fixedFrame > _startedDashing + _stats.DashDurationFrames)
            {
                _dashing = false;
                _speed.x *= _stats.DashEndHorizontalMultiplier;
                _speed.y *= _stats.DashEndVerticalMultiplier;
                _canDash = true;
            }
        }
    }

    private bool _dealtDamageThisDash;
    private void MeleeAttackDash()
    {
        _stats.DashDurationFrames = 9;
        if (_dashing)
        {
            _isInvincible.Value = true;
            _speed = new Vector2(transform.localScale.x > 0 ? 1 : -1, 0) * _stats.DashVelocity;
            // Cancel when the time is out or we've reached our max safety distance
            if (_fixedFrame > _startedDashing + _stats.DashInvincibleFrames)
            {
                _isInvincible.Value = false;
            }
            if (_fixedFrame > _startedDashing + _stats.DashDurationFrames)
            {
                _dashing = false;
                _speed.x *= _stats.DashEndHorizontalMultiplier;
                _canDash = true;
            }
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (_dashing && _stats.Melee && _meleeDashType == MeleeDashType.AttackDash 
            && !_dealtDamageThisDash && _bossLayer == (_bossLayer | (1 << collision.gameObject.layer)))
        {
            _dealtDamageThisDash = true;
            collision.gameObject.GetComponent<DamageModule>().TakeDamage(_stats.DashDamage);
        }
    }

    private void RangedOmniTeleport()
    {
        if (_dashing)
        {
            if (_dashVel == Vector2.zero)
            {
                transform.position += new Vector3(transform.localScale.x > 0 ? 1 : -1, 0, 0) * _stats.TeleportDistance;
            }
            else
            {
                transform.position += new Vector3(_dashVel.x, _dashVel.y, 0).normalized * _stats.TeleportDistance;
            }
            _dashing = false;
            _speed.y = 0;
            _canDash = true;
            CheckCollisions();
            HandleCollisions();
        }
    }
    private void RangedChargeDash(float percent)
    {
        _stats.DashDurationFrames = 9;
        if (_dashing)
        {
            _isInvincible.Value = true;
            _speed = new Vector2(transform.localScale.x > 0 ? 1 : -1, 0) * _stats.DashVelocity;
            // Cancel when the time is out or we've reached our max safety distance
            if (_fixedFrame > _startedDashing + _stats.DashInvincibleFrames)
            {
                _isInvincible.Value = false;
            }
            if (_fixedFrame > _startedDashing + _stats.DashDurationFrames)
            {
                _dashing = false;
                _speed.x *= _stats.DashEndHorizontalMultiplier;
                _playerCombat.IncreaseCharge(percent);
                _canDash = true;
            }
        }
    }
    
    private void RangedVerticalTeleport()
    {
        if (_dashing)
        {
            RaycastHit2D hit2D = Physics2D.Raycast(transform.position, Vector2.up * _dashVel.y, 50, _stats.TeleportLayers);
            if(_dashVel.y == 0 || hit2D.collider == null)
            {
                _dashing = false;
                _canDash = true;
                _numDashes.Value++;
                _movementCooldown.Value = _stats.MovementCooldown;
                return;
            }
            Vector2 teleportPos = hit2D.point;
            teleportPos.y += ((BoxCollider2D)hit2D.collider).size.y * hit2D.collider.gameObject.transform.lossyScale.y;
            teleportPos.y += _col.size.y / 2 - _col.offset.y;
            transform.position = teleportPos;
            _dashing = false;
            _canDash = true;
            CheckCollisions();
            HandleCollisions();
        }
    }

    #endregion

    #region Falling

    private Vector2 _groundNormal;
    private bool _isFallingAfterKnockback;

    protected virtual void HandleFall()
    {
        if (_dashing) return;

        // Grounded & Slopes
        if (_grounded && _speed.y <= 0f)
        {
            _speed.y = _stats.GroundingForce;

            // We use a raycast here as the groundHits from capsule cast act a bit weird.
            var hit = Physics2D.Raycast(transform.position, Vector2.down, _col.size.y * transform.localScale.y / 2 + _stats.GrounderDistance * 2, ~_stats.PlayerLayer);
            if (hit.collider != null)
            {
                _groundNormal = hit.normal;

                if (!Mathf.Approximately(_groundNormal.y, 1f)) // on a slope
                {
                    _speed.y = _speed.x * -_groundNormal.x / _groundNormal.y;
                    if (_speed.x != 0) _speed.y += _stats.GroundingForce;
                }
            }
            else
                _groundNormal = Vector2.zero;

            return;
        }

        // In Air
        var fallSpeed = _stats.FallAcceleration;
        if (_endedJumpEarly && _speed.y > 0) fallSpeed *= _stats.JumpEndEarlyGravityModifier;
        _speed.y = Mathf.MoveTowards(_speed.y, -_stats.MaxFallSpeed, fallSpeed * Time.fixedDeltaTime);
    }

    #endregion

    protected virtual void ApplyVelocity()
    {
        if (!_isKnockedBack.Value)
        {
            _isFallingAfterKnockback = false;
            _rb.velocity = _speed + _currentExternalVelocity;
            if (!_stats.Melee && _isCharging.Value)
            {
                _rb.velocity *= _stats.ChargeSlow;
            }
        }
        else if(_rb.velocity.y < 0 )
        {
            if (!_isFallingAfterKnockback)
            {
                _speed = Vector2.zero;
                _isFallingAfterKnockback = true;
            }
            _rb.velocity = new Vector2(_rb.velocity.x, _speed.y);
        }
    }

    private void OnEnable()
    {
        _receiveSwapRequest.Subscribe(ReceiveSwapRequest);
        _swapCompleted.Subscribe(SwapCompleted);
        if (_stats.Melee)
        {
            _onDownAttackInAir.Subscribe(DownAttackAirBounce);
        }
        _actions.Enable();
    }

    private void OnDisable()
    {
        _receiveSwapRequest.Unsubscribe(ReceiveSwapRequest);
        _swapCompleted.Unsubscribe(SwapCompleted);
        if (_stats.Melee)
        {
            _onDownAttackInAir.Unsubscribe(DownAttackAirBounce);
        }
        _actions.Disable();
    }
}