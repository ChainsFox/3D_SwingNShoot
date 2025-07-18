using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Player_Assets.FinalCharacterController
{
    [DefaultExecutionOrder(-1)]
    public class PlayerController : MonoBehaviour
    {
        #region Class Variables
        [Header("Components")]
        [SerializeField] private CharacterController _characterController; //"_" - underscore to represent private member field(recommend for c#)
        [SerializeField] private Camera _playerCamera;
        public float RotationMismatch { get; private set; } = 0f;
        public bool IsRotatingToTarget { get; private set; } = false;


        [Header("Base Movement")]
        public float walkAcceleration = 25f;
        public float walkSpeed = 2f;
        public float runAcceleration = 35f;
        public float runSpeed = 4f;
        public float sprintAcceleration = 50;
        public float sprintSpeed = 7f;
        public float inAirAcceleration = 25f;
        public float drag = 20f;
        public float inAirDrag = 5f;
        public float gravity = 25f;
        public float terminalVelocity = 50f;
        public float jumpSpeed = 1.0f;
        public float movingThreshold = 0.01f;

        [Header("Animation")]
        public float playerModelRotationSpeed = 10f;
        public float rotateToTargetTime = 0.25f; //these 2 control how quickly the player is gonna rotate


        [Header("Camera Settings")]
        public float lookSenseH = 0.1f;
        public float lookSenseV = 0.1f;
        public float lookLimitV = 89f; //clamp how high/low of an angle we can look, so that we dont spin around weirdly

        [Header("Enviroment Details")]
        [SerializeField] private LayerMask _groundLayers;

        private PlayerLocomotionInput _playerLocomotionInput;
        private PlayerState _playerState;

        private Vector2 _cameraRotation = Vector2.zero;
        private Vector2 _playerTargetRotation = Vector2.zero; //need both camera and player rotation to animate correctly

        private bool _jumpedLastFrame = false;
        private bool _isRotatingClockwise = false;
        private float _rotatingToTargetTimer = 0f;
        private float _verticalVelocity = 0f;
        private float _antiBump;
        private float _stepOffSet;

        private PlayerMovementState _lastMovementState = PlayerMovementState.Falling;

        #endregion

        #region Startup
        private void Awake()
        {
            _playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
            _playerState = GetComponent<PlayerState>();


            _antiBump = sprintSpeed; //we want the antiBump equal to our fastest possible velocity so that if we running down a 45 degree slope we wont skip(like skip a step)
            _stepOffSet = _characterController.stepOffset;
        }
        #endregion

        #region Update Logic
        private void Update()
        {
            UpdateMovementState();
            HandleVerticalMovement();
            HandleLateralMovement();

        }

        private void UpdateMovementState()
        {
            _lastMovementState = _playerState.CurrentPlayerMovementState; //to save our previous state 

            bool canRun = CanRun();
            bool isMovementInput = _playerLocomotionInput.MovementInput != Vector2.zero; //order matters
            bool isMovingLaterally = IsMovingLaterally();
            bool isSprinting = _playerLocomotionInput.SprintToggledOn && isMovingLaterally;
            bool isWalking = isMovingLaterally && (!canRun || _playerLocomotionInput.WalkToggleOn);
            //bool isWalking = (isMovingLaterally && !canRun) ||  _playerLocomotionInput.WalkToggleOn; //old wrong one
            bool isGrounded = IsGrounded();

            PlayerMovementState lateralState =  isWalking ? PlayerMovementState.Walking :
                                                isSprinting ? PlayerMovementState.Sprinting : //check if we are sprinting, if not we are running or idling
                                                isMovingLaterally || isMovementInput ? PlayerMovementState.Running : PlayerMovementState.Idling; //if we moving laterally or there is movement input, then we are in running state, else we are idling
            
            _playerState.SetPlayerMovementState(lateralState);

            //Control Airborn State
            if((!isGrounded || _jumpedLastFrame) && _characterController.velocity.y >= 0)
            {
                _playerState.SetPlayerMovementState(PlayerMovementState.Jumping);
                _jumpedLastFrame = false;
                _characterController.stepOffset = 0f;
            }
            else if((!isGrounded || _jumpedLastFrame) && _characterController.velocity.y < 0)
            {
                _playerState.SetPlayerMovementState(PlayerMovementState.Falling);
                _jumpedLastFrame = false;
                _characterController.stepOffset = 0f;
            }
            else
            {
                _characterController.stepOffset = _stepOffSet;
            }

        }

        private void HandleVerticalMovement()
        {
            bool isGrounded = _playerState.InGroundedState();
            
            _verticalVelocity -= gravity * Time.deltaTime;

            if (isGrounded && _verticalVelocity < 0)
                _verticalVelocity = -_antiBump; //we dont want to be moving down if we already grounded



            if(_playerLocomotionInput.JumpPressed && isGrounded)
            {
                _verticalVelocity += Mathf.Sqrt(jumpSpeed * 3 * gravity);
                _jumpedLastFrame = true;
            } 
                
            if(_playerState.IsStateGroundedState(_lastMovementState) && !isGrounded)//check if last state was grounded or not and check if we are switching from grounded to not grounded, if it was, then do the following below
            {
                _verticalVelocity += _antiBump;//add antibump to vertical velocity to offset the antibump value
            }

            if(Mathf.Abs(_verticalVelocity) > Mathf.Abs(terminalVelocity))
            {
                _verticalVelocity = -1f * Mathf.Abs(terminalVelocity); //cap the maxium velocity that we can reach for positive and negative numbers(up and down), so that we dont go into the speed force
            }

        }

        private void HandleLateralMovement()
        {
            //Create quick references for current state
            bool isSprinting = _playerState.CurrentPlayerMovementState == PlayerMovementState.Sprinting;
            bool isGrounded = _playerState.InGroundedState();
            bool isWalking = _playerState.CurrentPlayerMovementState == PlayerMovementState.Walking;

            //State  dependent acceleration and speed
            float lateralAcceleration = !isGrounded ? inAirAcceleration :
                                        isWalking ? walkAcceleration :
                                        isSprinting ? sprintAcceleration : runAcceleration; //if we sprinting we going at sprint acceleration, else, run acceleration
            float clampLateralMagnitude =   !isGrounded ? sprintSpeed :
                                            isWalking ? walkSpeed :
                                            isSprinting ? sprintSpeed : runSpeed; //same like above but with sprint/run speed 


            Vector3 cameraForwardXZ = new Vector3(_playerCamera.transform.forward.x, 0f, _playerCamera.transform.forward.z).normalized;
            Vector3 cameraRightXZ = new Vector3(_playerCamera.transform.right.x, 0f, _playerCamera.transform.right.z).normalized;
            Vector3 movementDirection = cameraRightXZ * _playerLocomotionInput.MovementInput.x + cameraForwardXZ * _playerLocomotionInput.MovementInput.y; //"movementDirection" - multiply the camera current facing direction with our movement input ->The direction we move to, is base on the camera

            Vector3 movementDelta = movementDirection * lateralAcceleration * Time.deltaTime; //movementDelta - is how much our player move this frame
            Vector3 newVelocity = _characterController.velocity + movementDelta; //move the player into new velocity/position

            //Add drag to player
            float dragMagnitude = isGrounded ? drag : inAirDrag; //if we grounded, use normal drag, if not we use air drag
            Vector3 currentDrag = newVelocity.normalized * dragMagnitude * Time.deltaTime;
            newVelocity = (newVelocity.magnitude > dragMagnitude * Time.deltaTime) ? newVelocity - currentDrag : Vector3.zero; //this is a ternary operator(basically a if else statement in 1 line)
            newVelocity = Vector3.ClampMagnitude(new Vector3(newVelocity.x, 0f, newVelocity.z), clampLateralMagnitude);// to make sure our acceleration doesn't go further than our maxium run speed
            newVelocity.y += _verticalVelocity;
            newVelocity = !isGrounded ? HandleSteepWalls(newVelocity) : newVelocity;  


            //Move charater (unity suggest only calling this once per tick)
            _characterController.Move(newVelocity * Time.deltaTime); //physically move the player
        }

        private Vector3 HandleSteepWalls(Vector3 velocity)
        {
            Vector3 normal = CharacterControllerUtils.GetNormalWithSphereCast(_characterController, _groundLayers); 
            float angle = Vector3.Angle(normal, Vector3.up);
            bool validAngle = angle <= _characterController.slopeLimit;

            if(!validAngle && _verticalVelocity < 0f)
            {
                velocity = Vector3.ProjectOnPlane(velocity, normal);
            }

            return velocity;
        }

        #endregion


        #region Late Update Logic
        private void LateUpdate()
        {
            UpdateCameraRotation();
        }


        private void UpdateCameraRotation()
        {
            //Camera Logic/rotation is recommended after the movement logic   

            _cameraRotation.x += lookSenseH * _playerLocomotionInput.LookInput.x;
            _cameraRotation.y = Mathf.Clamp(_cameraRotation.y - lookSenseV * _playerLocomotionInput.LookInput.y, -lookLimitV, lookLimitV);

            _playerTargetRotation.x += transform.eulerAngles.x + lookSenseH * _playerLocomotionInput.LookInput.x;

            

            float rotationTolerance = 90f; //rotation threshold/limit
            bool isIdling = _playerState.CurrentPlayerMovementState == PlayerMovementState.Idling;
            IsRotatingToTarget = _rotatingToTargetTimer > 0f;

            //rotate if we're not idling 
            if (!isIdling)
            {
                RotatePlayerToTarget();


            }
            //if rotation mismatch not within tolerance, or rotate to target is active, ROTATE
            else if (!isIdling || Mathf.Abs(RotationMismatch) > rotationTolerance || IsRotatingToTarget)
            {
                UpdateIdleRotation(rotationTolerance);
            }

            _playerCamera.transform.rotation = Quaternion.Euler(_cameraRotation.y, _cameraRotation.x, 0f);

            //get angle between camera and player, update rotation mismatch(IMPORTANT - Try to understand this if you can)
            Vector3 camForwardProjectedXZ = new Vector3(_playerCamera.transform.forward.x, 0f, _playerCamera.transform.forward.z).normalized; //XZ mean in the xz plane(look at picture online)
            Vector3 crossProduct = Vector3.Cross(transform.forward, camForwardProjectedXZ);
            float sign = Mathf.Sign(Vector3.Dot(crossProduct, transform.up));
            RotationMismatch = sign * Vector3.Angle(transform.forward, camForwardProjectedXZ);

        }

        private void UpdateIdleRotation(float rotationTolerance)
        {
            //innitiate new rotation direction 
            if (Mathf.Abs(RotationMismatch) > rotationTolerance)//when the camera > 90f, the player will start to rotate in this amount of time(rotate to target time), and will be reset again after it reaches 0
            {
                _rotatingToTargetTimer = rotateToTargetTime;
                _isRotatingClockwise = RotationMismatch > rotationTolerance;
            }
            _rotatingToTargetTimer -= Time.deltaTime;

            //rotate player
            if(_isRotatingClockwise && RotationMismatch > 0f || 
                !_isRotatingClockwise && RotationMismatch < 0f) //to determine if we need to keep rotating in that direction or stop to synch with animation
            {

                RotatePlayerToTarget();


            }


        }

        private void RotatePlayerToTarget()
        {
            Quaternion targetRotationX = Quaternion.Euler(0f, _playerTargetRotation.x, 0f);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotationX, playerModelRotationSpeed * Time.deltaTime);
        }

        #endregion

        #region State Checks
        private bool IsMovingLaterally()
        {
            Vector3 lateralVelocity = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z);

            return lateralVelocity.magnitude > movingThreshold;
        }

        private bool IsGrounded()
        {
            bool grounded = _playerState.InGroundedState() ? isGroundedWhileGrounded() : isGroundedWhileAirborne();

            return grounded;
        }

        private bool isGroundedWhileGrounded()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - _characterController.radius, transform.position.z);

            bool grounded =  Physics.CheckSphere(spherePosition, _characterController.radius, _groundLayers, QueryTriggerInteraction.Ignore);

            return grounded;
        }

        private bool isGroundedWhileAirborne()
        {
            Vector3 normal = CharacterControllerUtils.GetNormalWithSphereCast(_characterController, _groundLayers); 
            float angle = Vector3.Angle(normal, Vector3.up);
            //print(angle);
            bool validAngle = angle <= _characterController.slopeLimit;

            return _characterController.isGrounded && validAngle; //Explain: land on slope with 45 degrees or less for us to be grounded, otherwise we gonna keep sliding down to the ground
        }


        private bool CanRun()
        {
            //this means player is moving diagonally at 45 degress or forward, if so, we we can run.
            //(Imagine a circle, and there a pizza shape in the top part of that circle, if you move forward, or move in the area of the pizza shape, that means you can run)
            return _playerLocomotionInput.MovementInput.y >= Mathf.Abs(_playerLocomotionInput.MovementInput.x);
        }

        #endregion



    }



}

