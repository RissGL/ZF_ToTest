using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using ZGameFramework;
using System.Collections;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    public class GravityAniController:MonoBehaviour, IController
    {
        private IGravityAniModel  gravityAniModel;
        private Camera camera;
        
        private bool isPlaying=false;
        [SerializeField]private List<MangaPanelSingle>  mangaSingles;
        private float moveDuration=0.5f;

        [SerializeField] private MangaCameraController  mangaCameraController;
        
        [SerializeField] private SimpleAudioEventExample onNextTexSoundEff;
        
        private void Awake()
        {
            camera = Camera.main;

            gravityAniModel = this.GetModel<IGravityAniModel>();
            gravityAniModel.Setup(mangaSingles.Count);
            gravityAniModel.CurrentIndex.Register(OnNextTex).UnregisterOnDestroyTrigger(this);
            
            /*this.RegisterEvent<NextTexEvent>(OnNextTex).UnregisterOnDestroyTrigger(this);*/
            
            mangaCameraController.SetMangaSingles(mangaSingles);

        }

        private void Start()
        {
            StartCoroutine(RendererFirstFrame());
        }

        private IEnumerator  RendererFirstFrame()
        {
            yield return null;
            foreach (var mangaSingle in mangaSingles)
            {
                mangaSingle.SetCameraActive(false);
            }
            mangaSingles[0].SetCameraActive(true);
        }


        private void OnNextTex(int index)
        {
            moveDuration = mangaSingles[index].moveToDuration;
            var targetPosition = mangaSingles[index].GetPosition()
                +mangaSingles[index].transform.forward
                *mangaSingles[index].GetCameraDistance();
            isPlaying = true;

            foreach (var mangaSingle in mangaSingles)
            {
                mangaSingle.SetCameraActive(false);
            }
            mangaSingles[index].SetCameraActive(true);
            
            camera.transform.DOMove(targetPosition, moveDuration)
                .OnStart(()=>mangaSingles[index].PlayAni())
                .OnComplete(() =>
                {
                    isPlaying = false;
                    this.SendEvent<TexChangeEnd>();
                });
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.K)&&!isPlaying)
            {
                NextTex();
            }
        }

        public void NextTex()
        {
            onNextTexSoundEff?.Play();
            this.SendCommand<NextTexCommand>();
        }

        public MangaPanelSingle GetMangaSingle(int mangaIndex)
            => mangaSingles[mangaIndex];

        public IArchitecture GetArchitecture()
        {
            return GravityAniApp.Interface;
        }
    }
}