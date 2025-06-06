using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


namespace Player_Assets.FinalCharacterController
{
    [DefaultExecutionOrder(-2)] //this script always run before other scripts
    public class PlayerLocomotionInput : MonoBehaviour, PlayerControls.IPlayerLocomotionMapActions
    {
        #region Class Variables
        [SerializeField] private bool holdToSprint = true;

        public Vector2 MovementInput { get; private set; }

        public Vector2 LookInput { get; private set; }

        public bool JumpPressed { get; private set; }

        public bool SprintToggledOn { get; private set; }
        public bool WalkToggleOn { get; private set; }
        #endregion

        #region Startup
        private void OnEnable()
        {
            if(PlayerInputManager.Instance?.PlayerControls == null) //"PlayerInputManager.Instance?" - this is a null conditional operators, check if instance is not null, then check if playercontrols is null
            {
                Debug.LogError("Player controls is not initialized - cannot enable"); //guard check to make sure player controls is initialize/if either instance or player controls is null then print warning
                return; //return if this condition is true so that the code below doesnt run
            }


            PlayerInputManager.Instance.PlayerControls.PlayerLocomotionMap.Enable();
            PlayerInputManager.Instance.PlayerControls.PlayerLocomotionMap.SetCallbacks(this);

        }

        private void OnDisable()
        {
            if (PlayerInputManager.Instance?.PlayerControls == null)
            {
                Debug.LogError("Player controls is not initialized - cannot disable");
                return;
            }

            PlayerInputManager.Instance.PlayerControls.PlayerLocomotionMap.Disable();
            PlayerInputManager.Instance.PlayerControls.PlayerLocomotionMap.RemoveCallbacks(this);
        }
        #endregion

        #region Late Update Logic
        private void LateUpdate()
        {
            JumpPressed = false;
        }
        #endregion

        #region Input Callbacks
        public void OnMovement(InputAction.CallbackContext context)
        {
            MovementInput = context.ReadValue<Vector2>(); //we printing out direction in a vector 2 format - use to control player movement
            //print(MovementInput);
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            LookInput = context.ReadValue<Vector2>();
        }

        public void OnToggleSprint(InputAction.CallbackContext context)
        {
            if(context.performed)
            {
                SprintToggledOn = holdToSprint || !SprintToggledOn; //hold to sprint is true or sprint toggled on false
            }
            else if(context.canceled)
            {
                SprintToggledOn = !holdToSprint && SprintToggledOn;
            }
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (!context.performed) //we dont do any of the code below "return", if space bar isn't held down
                return;

            JumpPressed = true;
        }

        public void OnToggleWalk(InputAction.CallbackContext context)
        {
            if (!context.performed) //if we not pressing down our walk key then we dont do anything, otherwise switch WalkToggleOn from true to false and vice versa
                return;

            WalkToggleOn = !WalkToggleOn;
        }
        #endregion

    }



}
