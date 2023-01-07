using Assets.Scripts.being;
using Assets.Scripts.utils;
using Cinemachine;
using UnityEngine;

namespace Assets.Scripts.managers
{
    public class GeneralData : Singleton<GeneralData>
    {
        [Tooltip("Player")]
        public CharacterMover player;

        [Tooltip("Main Camera")]
        public Camera mainCamera;

        [Tooltip("Planning Ample Camera")]
        public Camera ampleCamera;

        [Tooltip("Where Jumpers Go Into")]
        public GameObject jumpersFolder;

        public CinemachineVirtualCamera AmpleCameraCineMachine { get; private set; }

        private new void Awake()
        {
            AmpleCameraCineMachine = ampleCamera.GetComponent<CinemachineVirtualCamera>();
        }
    }
}