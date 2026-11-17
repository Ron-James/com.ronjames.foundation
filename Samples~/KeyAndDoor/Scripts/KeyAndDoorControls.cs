using RonJames.Foundation.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RonJames.Foundation.Samples.KeyAndDoor
{
    /// <summary>
    /// Inspector buttons for driving the sample by hand in play mode. Everything else in the scene is
    /// wired through assets: nothing here knows about the door.
    /// </summary>
    public class KeyAndDoorControls : MonoBehaviour
    {
        [SerializeField]
        private BoolVariableEvent hasKey = null;

        [SerializeField]
        private EventAsset doorBell = null;

        [SerializeField]
        private GameObject key = null;

        [Button(ButtonSizes.Large)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void PickUpKey()
        {
            if (key != null)
            {
                key.SetActive(false);
            }

            hasKey.Set(true);
        }

        [Button(ButtonSizes.Large)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void RingDoorBell()
        {
            doorBell.Raise();
        }
    }
}
