// Assets/Scripts/Gameplay/HandModelNetworkAttach.cs
using System.Collections;
using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public class HandModelNetworkAttach : NetworkBehaviour
    {
        public override void Spawned()
        {
            // При спавне пускаем корутину, чтобы дождаться готовности InteractionController
            StartCoroutine(AttachWhenReady());
        }

        private IEnumerator AttachWhenReady()
        {
            // 1) ждём, пока на этой сцене появится нужный InteractionController
            InteractionController ic = null;
            while (ic == null)
            {
                // ждём конца кадра, чтобы Fusion успел заспавнить все объекты
                yield return new WaitForEndOfFrame();
                foreach (var cand in FindObjectsOfType<InteractionController>())
                {
                    // ищем того, чей InputAuthority совпадает с нашим владельцем модели
                    if (cand.Object.InputAuthority == Object.InputAuthority)
                    {
                        ic = cand;
                        break;
                    }
                }
            }

            // 2) attach
            transform.SetParent(ic.HandPoint, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }
}
