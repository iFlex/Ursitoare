using Unity.Cinemachine;
using UnityEngine;

namespace DefaultNamespace
{
    public class SingletonUtils: MonoBehaviour
    {
        public static SingletonUtils instance;
        public CinemachineCamera camera;

        void Awake()
        {
            instance = this;
        }
    }
}