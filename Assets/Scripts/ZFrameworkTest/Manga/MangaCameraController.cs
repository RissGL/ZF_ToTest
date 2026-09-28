using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using ZGameFramework;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    public class  MangaCameraController : MonoBehaviour,IController
    {
        [SerializeField] private Camera camera;
        
        [Header("目标")]
        [SerializeField] private Transform target;

        [Header("旋转范围")]
        [SerializeField] private float maxYaw = 15f;
        [SerializeField] private float maxPitch = 10f;

        [Header("输入灵敏度")]
        [SerializeField] private float sensitivity = 2f;

        [Header("回弹")]
        [Label("平滑时间")]
        [SerializeField] private float smoothTime = 0.2f;
        [Label("无输入回正速度")]
        [SerializeField] private float unInputBackSpeed = 1f;
        [Label("停留时间")]
        [SerializeField] private float unInputStayTime = 0.2f;

        private float currentStayTime=0;
        
        private float targetYaw;
        private float targetPitch;

        private float currentYaw;
        private float currentPitch;

        private float yawVelocity;
        
        private float pitchVelocity;
        
        private List<MangaPanelSingle> mangaSingles;
        private bool isChanging=false;

        private MangaPanelSingle targetMangaSingle;
        private IGravityAniModel  gravityAniModel;
        
        
        public void SetMangaSingles(List<MangaPanelSingle> mangaSingles)
        {
            this.mangaSingles = mangaSingles;
            targetMangaSingle=mangaSingles[0];
        }

        private void Start()
        {
            mangaSingles = mangaSingles;
            this.RegisterEvent<TexChangeEnd>(() => isChanging = false)
                .UnregisterOnDestroyTrigger(this);

            gravityAniModel = this.GetModel<IGravityAniModel>();
            gravityAniModel.CurrentIndex.Register(OnNextTex).UnregisterOnDestroyTrigger(this);
            /*
            this.RegisterEvent<NextTexEvent>(OnNextTex)
                .UnregisterOnDestroyTrigger(this);*/
        }

        private int index = 0;
        private void OnNextTex(int  index)
        { 
            Debug.Log(index+"事件收到");
            var targetManga = mangaSingles[index];
            target = targetManga.transform;
            targetMangaSingle=targetManga;
            this.index = index;
        }

        private void LateUpdate()
        {
            if (isChanging)
            {
                return;
            }
            
            Vector2 input = Mouse.current.delta.ReadValue();

            if (input.sqrMagnitude > 0.01f)
            {
                targetYaw += input.x * sensitivity * Time.deltaTime;
                targetPitch -= input.y * sensitivity * Time.deltaTime;
                currentStayTime = 0;
            }
            else if(input.sqrMagnitude <= 0.01f)
            {
                currentStayTime+=Time.deltaTime;
                if (currentStayTime>=unInputStayTime)
                {
                    // 没有输入的时候，目标位置回到中心
                    targetYaw = Mathf.Lerp(targetYaw, 0f, unInputBackSpeed * Time.deltaTime);
                    targetPitch = Mathf.Lerp(targetPitch, 0f, unInputBackSpeed * Time.deltaTime);
                }
            }

            targetYaw = Mathf.Clamp(targetYaw, -maxYaw, maxYaw);
            targetPitch = Mathf.Clamp(targetPitch, -maxPitch, maxPitch);

            currentYaw = Mathf.SmoothDamp(
                currentYaw,
                targetYaw,
                ref yawVelocity,
                smoothTime
            );

            currentPitch = Mathf.SmoothDamp(
                currentPitch,
                targetPitch,
                ref pitchVelocity,
                smoothTime
            );
            
            // 应用旋转
            
            var rotationOffset = Quaternion.Euler(targetYaw, targetPitch, 0);
            camera.transform.rotation =
                target.rotation * rotationOffset;
            UpdateTargetMangaRotation(rotationOffset);
        }

        public void UpdateTargetMangaRotation(Quaternion offset)
        {
            Debug.Log(index);
            targetMangaSingle.SetRotationOffset(offset);
        }

        public IArchitecture GetArchitecture()
        {
            return GravityAniApp.Interface;
        }
    }
}