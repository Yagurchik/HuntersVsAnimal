using UnityEngine;

public class CameraBehaviour : MonoBehaviour
{
    [SerializeField] private Transform stage2_1;
    [SerializeField] private Transform stage2_2;
    [SerializeField] private Transform stage2_3;
    private Vector3 stage1;
    public Transform CurrentRoom { get; private set; }
    public bool IsTransitioning
    {
        get
        {
            CameraRenderer framing = GetComponent<CameraRenderer>();
            return framing != null && framing.IsTransitioning;
        }
    }
    public event System.Action<Stage> RoomChanged;
    private void Awake()
    {
        stage1 = transform.position;
        transform.rotation = Quaternion.identity;
    }
    public void Stage1()
    {
        if (!CanSwitchRoom())
        {
            return;
        }

        Stage room = FindStage("Stage 1");
        if (room != null)
        {
            FocusRoom(room);
            return;
        }

        FocusPosition(stage1);
    }
    public void Stage2_1()
    {
        FocusLegacyRoom(stage2_1);
    }
    public void Stage2_2()
    {
        FocusLegacyRoom(stage2_2);
    }
    public void Stage2_3()
    {
        FocusLegacyRoom(stage2_3);
    }

    public void FocusRoom(Stage room)
    {
        if (!CanSwitchRoom())
        {
            return;
        }

        LevelGenerator generator = room != null ? room.GetComponentInChildren<LevelGenerator>(true) : null;
        if (generator != null && GameManager.Instance?.Front != null
            && !GameManager.Instance.Front.CanSelect(generator.RoomId)) return;

        SetRoom(room);
    }

    public void ForceFocusRoom(Stage room)
    {
        SetRoom(room, false);
    }

    public void ForceFocusRoomAnimated(Stage room) => SetRoom(room, true);

    private void SetRoom(Stage room)
    {
        SetRoom(room, false);
    }

    private void SetRoom(Stage room, bool animated)
    {
        if (room == null)
        {
            return;
        }

        LevelGenerator target = room.GetComponentInChildren<LevelGenerator>(true);
        DefenseLineRuntime front = GameManager.Instance != null ? GameManager.Instance.Front : null;
        if (front != null && target != null && !front.CanFocusWhileDefending(target.RoomId))
            return;

        animated &= CurrentRoom != room.transform;

        Stage[] allRooms = FindObjectsOfType<Stage>(true);
        for (int i = 0; i < allRooms.Length; i++)
        {
            allRooms[i].SetPlayerActive(allRooms[i] == room);
        }

        CurrentRoom = room.transform;
        CameraRenderer framing = GetComponent<CameraRenderer>();
        if (framing != null)
        {
            framing.FrameRoom(room, animated);
        }
        else
        {
            Vector3 position = room.transform.position;
            // Room roots describe the board plane, not the camera's viewing depth.
            position.z = transform.position.z;
            transform.SetPositionAndRotation(position, Quaternion.identity);
        }
        RoomChanged?.Invoke(room);
    }

    private void FocusLegacyRoom(Transform roomTransform)
    {
        if (!CanSwitchRoom() || roomTransform == null)
        {
            return;
        }

        Stage room = roomTransform.GetComponent<Stage>();
        if (room != null)
        {
            FocusRoom(room);
            return;
        }

        FocusPosition(roomTransform.position);
    }

    private void FocusPosition(Vector3 position)
    {
        if (GameManager.Instance?.Front?.IsDefendingRoom == true) return;
        CurrentRoom = null;
        position.z = transform.position.z;
        transform.SetPositionAndRotation(position, Quaternion.identity);
        // Clear cached room bounds so LateUpdate cannot undo a legacy transition.
        GetComponent<CameraRenderer>()?.FrameRoom(null);
    }

    private static Stage FindStage(string roomName)
    {
        Stage[] allRooms = FindObjectsOfType<Stage>(true);
        for (int i = 0; i < allRooms.Length; i++)
        {
            if (allRooms[i].gameObject.name == roomName)
            {
                return allRooms[i];
            }
        }

        return null;
    }

    private static bool CanSwitchRoom()
    {
        return GameManager.Instance == null || GameManager.Instance.CanSwitchRooms;
    }
}
