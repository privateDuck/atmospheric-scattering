using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace StormAtmosphere
{
#if UNITY_EDITOR
    [ExecuteInEditMode]
    public class Editor_AutoUpdateHelper : MonoBehaviour
    {
        public Action OnUpdate;
        public Action ReapeatingInvoke;
        private float time = 0f;
        public void OnEnable()
        {
            time = 0f;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                OnUpdate?.Invoke();
                time += Time.deltaTime;
                if (time > 4f)
                {
                    time = 0f;
                    ReapeatingInvoke?.Invoke();
                }
            }
        }

    }
#endif
}