using UnityEngine;
#if CINEMACHINE
using Cinemachine;
#endif

[DisallowMultipleComponent]
public class ThirdPersonCam : MonoBehaviour
{
#if CINEMACHINE
    [Tooltip("Virtual Camera to control. If null, will try to find first CinemachineVirtualCamera in scene.")]
    public CinemachineVirtualCamera virtualCamera;

    [Tooltip("Transform to set as Follow/LookAt (usually your Player).")]
    public Transform followTarget;

    private void Start()
    {
        if (virtualCamera == null)
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();

        if (virtualCamera != null && followTarget != null)
        {
            virtualCamera.Follow = followTarget;
            virtualCamera.LookAt = followTarget;
        }
    }
#else
    private void Start()
    {
        Debug.LogWarning("Cinemachine is not installed in the project. Install Cinemachine package to use CinemachineFollow.");
    }
#endif
}