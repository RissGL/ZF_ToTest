using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZF.DialoguePresentation
{

    public class MangaPanelSingle:MonoBehaviour
    {
        [SerializeField] private float cameraDistance=1.0f;
        [SerializeField] private Camera mangaCamera;
        [SerializeField] private Transform targetTransform;

        public float moveToDuration { get; private set; } = 0.5f;

        private ShowAniComponent playAni;
        private Quaternion rotationOffset=Quaternion.identity;

        private void Awake()
        {
            playAni = this.GetComponent<ShowAniComponent>();
        }

        private void Update()
        {
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            mangaCamera.transform.rotation = rotationOffset;
        }

        public void SetRotationOffset(Quaternion offset)
        {
            rotationOffset = offset;
        }

        public Vector3 GetPosition()=>targetTransform.position;
        public float GetCameraDistance()=>cameraDistance;
        public void SetCameraActive(bool isActive)=>mangaCamera.enabled=isActive;
        
        public void PlayAni()
        {
            if (playAni!=null)
            {
                playAni.Play();
            }
        }
    }
    
    
}

