using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZGameFramework.Utility
{
    public class LookAtCamera : MonoBehaviour
    {
        private enum Mode
        {
            LookAt,
            LookAtReverse,
            ForWard,
            ForWardReverse,
        }

        [Label("朝向模式")]
        [SerializeField] private Mode mode;

        [SerializeField] private Camera camera;
        private Camera targetCamera
        {
            get
            {
                if (camera==null)
                {
                    camera = Camera.main;
                }

                return camera;
            }
             set { }
        }

        private void LateUpdate()
        {
            switch (mode)
            {
                case Mode.LookAt:
                    transform.LookAt(targetCamera.transform);
                    break;
                case Mode.LookAtReverse:
                    Vector3 dirDistance = transform.position - targetCamera.transform.forward;
                    transform.LookAt(dirDistance);
                    break;
                case Mode.ForWard:
                    transform.forward = targetCamera.transform.forward;
                    break;
                case Mode.ForWardReverse:
                    transform.forward = -targetCamera.transform.forward;
                    break;
            }
        }
    }
}
